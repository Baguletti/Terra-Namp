using Microsoft.Xna.Framework.Audio;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using XPT.Core.Audio.MP3Sharp;

namespace Terra_Namp.Core.Audio
{
    public class DynamicAudioTrack : ASoundEffectBasedAudioTrack
    {
        private readonly Stream stream;

        private readonly MP3Stream mp3Stream;

        private readonly long frequency;

        // This is derived from ffmpeg encoding all incoming files at 320kbps (40kHz).
        private const int Bitrate = 40000;

        private float normalizationGain = 1f;
        private volatile bool disposed;

        private readonly ReverbProcessor reverbProcessor = new();
        private bool slowedReverbEnabled;
        private const float SlowedPitchValue = -0.25f;

        /// <summary>
        /// Smooth volume fade: 0→1 over 0.3 seconds (18 ticks at 60fps).
        /// Eliminates clicks on pause/resume.
        /// </summary>
        private const float FadeDuration = 0.3f; // seconds
        private float targetVolumeFade = 1f;
        private float currentVolumeFade = 1f;
        private float lastUserVolume = 1f;

        // PCM-level micro-fade to eliminate decoder warmup clicks and end-of-stream pops
        private bool needsMicroFadeIn;
        private const int MicroFadeMs = 30; // 30ms ramp — covers full MP3 frame (1152 samples = 28.8ms at 40kHz)

        public bool SlowedReverbEnabled
        {
            get => slowedReverbEnabled;
            set
            {
                slowedReverbEnabled = value;
                _soundEffectInstance.Pitch = value ? SlowedPitchValue : 0f;
                if (!value)
                    reverbProcessor.Reset();
            }
        }

        public float PitchFactor => slowedReverbEnabled ? MathF.Pow(2f, SlowedPitchValue) : 1f;

        /// <summary>
        /// UI volume (0.0 - 1.0). Applied via _soundEffectInstance.Volume with smooth fade.
        /// </summary>
        public float Volume
        {
            get => lastUserVolume;
            set
            {
                lastUserVolume = value;
                // If already at target fade, apply immediately
                if (Math.Abs(currentVolumeFade - targetVolumeFade) < 0.001f)
                    _soundEffectInstance.Volume = value * currentVolumeFade;
            }
        }

        public byte[] BufferToSubmit => _bufferToSubmit ?? Array.Empty<byte>();

        public float Progress => mp3Stream.Length > 0 ? (float)mp3Stream.Position / mp3Stream.Length : 0f;

        public TimeSpan ElapsedTime => SongDuration * Progress;

        public TimeSpan SongDuration => TimeSpan.FromSeconds(mp3Stream.Length / Bitrate / (double)PitchFactor);

        public DynamicAudioTrack(Stream stream)
        {
            this.stream = stream;

            MP3Stream mp3Stream = new(stream);

            frequency = mp3Stream.Frequency;

            this.mp3Stream = mp3Stream;

            CreateSoundEffect((int)frequency, AudioChannels.Stereo);

            // Compute normalization gain asynchronously to avoid blocking track switch
            // TEMPORARILY DISABLED FOR TESTING
            // string filePath = stream is FileStream fs ? fs.Name : null;
            // if (filePath != null)
            // {
            //     Task.Run(() =>
            //     {
            //         normalizationGain = ComputeNormalizationGain(filePath);
            //     });
            // }
        }

        /// <summary>
        /// Scans the entire decoded PCM stream to measure RMS loudness and peak,
        /// then computes a gain factor to normalize to -16 dBFS RMS with peak limiting.
        /// </summary>
        private static float ComputeNormalizationGain(string filePath)
        {
            try
            {
                using var scanStream = File.OpenRead(filePath);
                using var scanMp3 = new MP3Stream(scanStream);

                byte[] scanBuffer = new byte[16384];
                double sumSquares = 0;
                long sampleCount = 0;
                float maxPeak = 0f;
                int bytesRead;

                while ((bytesRead = scanMp3.Read(scanBuffer, 0, scanBuffer.Length)) > 0)
                {
                    for (int i = 0; i + 1 < bytesRead; i += 2)
                    {
                        short sample = (short)(scanBuffer[i] | (scanBuffer[i + 1] << 8));
                        float norm = sample / 32768f;
                        sumSquares += norm * norm;
                        sampleCount++;
                        float abs = Math.Abs(norm);
                        if (abs > maxPeak) maxPeak = abs;
                    }
                }

                if (sampleCount == 0 || maxPeak < 0.01f) return 1f;

                float rms = (float)Math.Sqrt(sumSquares / sampleCount);
                if (rms < 0.001f) return 1f;

                // Target RMS: -16 dBFS (≈0.158), similar to Spotify/YouTube normalization
                const float targetRms = 0.158f;
                float gain = targetRms / rms;

                // Prevent clipping: ensure peak * gain <= 0.95 (-0.45 dBFS headroom)
                float maxAllowedGain = 0.95f / maxPeak;
                gain = Math.Min(gain, maxAllowedGain);

                return Math.Clamp(gain, 0.1f, 4f);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Smoothly fades out volume to 0 over FadeDuration, then pauses.
        /// Call this instead of Pause() to eliminate clicks.
        /// </summary>
        public void PauseSmooth()
        {
            if (!IsPlaying) return;
            targetVolumeFade = 0f;
        }

        /// <summary>
        /// Resumes playback and smoothly fades in volume from 0 to 1 over FadeDuration.
        /// Call this instead of Resume() to eliminate clicks.
        /// </summary>
        public void ResumeSmooth()
        {
            if (!IsPaused) return;
            needsMicroFadeIn = true;
            currentVolumeFade = 0f;
            targetVolumeFade = 1f;
            _soundEffectInstance.Volume = 0f;
            Resume();
        }

        /// <summary>
        /// Immediately cancels any fade and ensures playback is running.
        /// Works regardless of whether track is mid-fade, paused, or playing.
        /// </summary>
        public void ForceResume()
        {
            targetVolumeFade = 1f;
            currentVolumeFade = 1f;
            _soundEffectInstance.Volume = lastUserVolume;

            if (IsPaused)
                Resume();
        }

        /// <summary>
        /// Starts playback with smooth fade-in from 0 to 1.
        /// Pre-fills the buffer before Play() to prevent buffer underrun clicks.
        /// </summary>
        public void PlaySmooth()
        {
            needsMicroFadeIn = true;
            lastUserVolume = 0f; // Prevent volume dip on first frame (will be set by SetVolume next frame)
            currentVolumeFade = 0f;
            targetVolumeFade = 1f;
            _soundEffectInstance.Volume = 0f;

            // Pre-fill buffer before starting playback — prevents the 1-2 frame
            // buffer underrun gap that causes a click on the audio hardware
            ReadAheadPutAChunkIntoTheBuffer();

            Play();
        }

        /// <summary>
        /// Updates smooth volume fade. Call this every frame (60fps).
        /// Uses cosine S-curve for perceptually smooth transitions —
        /// derivative is zero at endpoints, eliminating audible steps near silence and full volume.
        /// </summary>
        public void UpdateVolumeFade(float deltaTime)
        {
            if (Math.Abs(currentVolumeFade - targetVolumeFade) < 0.001f)
                return;

            float fadeSpeed = 1f / FadeDuration; // 1/0.3 = ~3.33 per second
            if (currentVolumeFade < targetVolumeFade)
            {
                currentVolumeFade = Math.Min(currentVolumeFade + fadeSpeed * deltaTime, targetVolumeFade);
            }
            else
            {
                currentVolumeFade = Math.Max(currentVolumeFade - fadeSpeed * deltaTime, targetVolumeFade);
            }

            // S-curve: 0.5 - 0.5*cos(t*PI) maps linear t to smooth curve with zero derivative at 0 and 1
            float smoothed = 0.5f - 0.5f * MathF.Cos(currentVolumeFade * MathF.PI);
            _soundEffectInstance.Volume = lastUserVolume * smoothed;

            // Auto-pause when fade-out completes
            if (targetVolumeFade == 0f && currentVolumeFade == 0f && IsPlaying)
            {
                Pause();
            }
        }


        public override void Reuse()
        {
            mp3Stream.Position = 0L;
        }

        public override void Dispose()
        {
            disposed = true;
            _soundEffectInstance.Dispose();
            mp3Stream.Dispose();
            stream.Dispose();
        }

        protected override void ReadAheadPutAChunkIntoTheBuffer()
        {
            if (disposed) return;

            try
            {
                byte[] bufferToSubmit = _bufferToSubmit;

                // Loop to fill buffer completely — partial reads leave stale data → clicks
                int totalRead = 0;
                while (totalRead < bufferToSubmit.Length)
                {
                    int bytesRead = mp3Stream.Read(bufferToSubmit, totalRead, bufferToSubmit.Length - totalRead);
                    if (bytesRead < 1)
                        break;
                    totalRead += bytesRead;
                }

                if (totalRead == 0)
                {
                    Stop(AudioStopOptions.Immediate);
                    return;
                }

                // Zero remainder to prevent stale data clicks at end-of-stream
                if (totalRead < bufferToSubmit.Length)
                    Array.Clear(bufferToSubmit, totalRead, bufferToSubmit.Length - totalRead);

                // Apply normalization gain to PCM samples
                if (Math.Abs(normalizationGain - 1f) > 0.001f)
                {
                    var samples = MemoryMarshal.Cast<byte, short>(bufferToSubmit.AsSpan());
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = (short)Math.Clamp(samples[i] * normalizationGain, -32768f, 32767f);
                }

                // Apply reverb when slowed+reverb mode is enabled
                if (slowedReverbEnabled)
                {
                    reverbProcessor.Process(MemoryMarshal.Cast<byte, short>(bufferToSubmit.AsSpan()));
                }

                // PCM micro-fade-in: ramp first ~30ms from 0→1 to eliminate MP3 decoder warmup artifacts
                if (needsMicroFadeIn)
                {
                    int fadeSamples = (int)(frequency * MicroFadeMs / 1000) * 2; // *2 for stereo interleaved
                    var samples = MemoryMarshal.Cast<byte, short>(bufferToSubmit.AsSpan(0, totalRead));
                    fadeSamples = Math.Min(fadeSamples, samples.Length);
                    for (int i = 0; i < fadeSamples; i++)
                        samples[i] = (short)(samples[i] * ((float)i / fadeSamples));
                    needsMicroFadeIn = false;
                }

                // PCM micro-fade-out at end of stream: ramp last ~30ms from 1→0 to eliminate end-of-song pop
                if (totalRead > 0 && totalRead < bufferToSubmit.Length)
                {
                    int fadeSamples = (int)(frequency * MicroFadeMs / 1000) * 2;
                    var samples = MemoryMarshal.Cast<byte, short>(bufferToSubmit.AsSpan(0, totalRead));
                    fadeSamples = Math.Min(fadeSamples, samples.Length);
                    int start = samples.Length - fadeSamples;
                    for (int i = 0; i < fadeSamples; i++)
                        samples[start + i] = (short)(samples[start + i] * (1f - (float)i / fadeSamples));
                }

                if (disposed) return;
                _soundEffectInstance.SubmitBuffer(bufferToSubmit);
            }
            catch (ObjectDisposedException)
            {
                // Stream or sound effect disposed during read — expected during rapid track switching
            }
            catch (Exception ex)
            {
                Terra_Namp.Instance?.Logger.Error($"ReadAheadPutAChunkIntoTheBuffer error: {ex.Message}");
                try { Stop(AudioStopOptions.Immediate); } catch { }
            }
        }

        public void Skip(double seconds)
        {
            long jumpInBytes = (long)(seconds * Bitrate);

            mp3Stream.Position = Math.Clamp(mp3Stream.Position + jumpInBytes, 0L, mp3Stream.Length);
            needsMicroFadeIn = true; // MP3 decoder needs 1-2 frames to re-sync after seek
        }

        public void SeekToProgress(float progress)
        {
            mp3Stream.Position = (long)(Math.Clamp(progress, 0f, 1f) * mp3Stream.Length);
            needsMicroFadeIn = true; // MP3 decoder needs 1-2 frames to re-sync after seek
        }
    }
}
