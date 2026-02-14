using Terra_Namp.Localization;
using Terra_Namp.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace Terra_Namp.Core.Audio
{
    public class DownloadJob
    {
        public string Url;
        public string Title;
        public float Progress;
        public string Speed;
        public string ETA;
        public string Phase;
        public bool IsComplete;
        public bool IsFailed;
        public string FailMessage;
        public string Uuid;
        public DateTime CompletedAt;
        internal CancellationTokenSource CTS = new();
        internal long LastActivityTicks = DateTime.UtcNow.Ticks;
        internal long ExpectedBytes;
        internal double Duration;

        internal void ResetActivity() =>
            Interlocked.Exchange(ref LastActivityTicks, DateTime.UtcNow.Ticks);

        internal double IdleSeconds
        {
            get
            {
                long ticks = Interlocked.Read(ref LastActivityTicks);
                return (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
            }
        }
    }

    public static class AsyncDownloader
    {
        private static readonly object _lock = new();
        private static readonly List<DownloadJob> _jobs = new();

        private const int InactivityTimeoutSeconds = 300;
        private const double CompletedDisplaySeconds = 5;

        public static IReadOnlyList<DownloadJob> GetJobs()
        {
            lock (_lock)
                return _jobs.ToArray();
        }

        /// <summary>
        /// Removes completed/failed jobs that have been visible for long enough.
        /// Call from UI update loop.
        /// </summary>
        public static void PruneFinishedJobs()
        {
            lock (_lock)
                _jobs.RemoveAll(j =>
                    (j.IsComplete || j.IsFailed) &&
                    (DateTime.UtcNow - j.CompletedAt).TotalSeconds > CompletedDisplaySeconds);
        }

        /// <summary>
        /// Cancels all active downloads and clears the job list.
        /// Call from Mod.Unload() to prevent background threads from surviving mod reload.
        /// </summary>
        public static void CancelAll()
        {
            lock (_lock)
            {
                foreach (var job in _jobs)
                    job.CTS.Cancel();
                _jobs.Clear();
            }
        }

        public static DownloadJob StartDownload(string url, Action<string> onComplete, Action<string> onFail)
        {
            var job = new DownloadJob
            {
                Url = url,
                Phase = "Starting...",
            };

            lock (_lock)
                _jobs.Add(job);

            // Main download task
            Task.Run(() => RunJobAsync(job, onComplete, onFail));

            // Per-job inactivity watchdog
            Task.Run(() => WatchdogAsync(job, onFail));

            return job;
        }

        private static async Task WatchdogAsync(DownloadJob job, Action<string> onFail)
        {
            while (!job.IsComplete && !job.IsFailed && !job.CTS.IsCancellationRequested)
            {
                await Task.Delay(1000);

                if (job.IdleSeconds > InactivityTimeoutSeconds)
                {
                    job.CTS.Cancel();
                    job.IsFailed = true;
                    job.FailMessage = LocalizationHelper.GetGUIText("AsyncMP3Downloader.ConnectionTimeout");
                    job.Phase = "Timeout";
                    job.CompletedAt = DateTime.UtcNow;
                    onFail?.Invoke(job.FailMessage);
                    return;
                }
            }
        }

        private static async Task RunJobAsync(DownloadJob job, Action<string> onComplete, Action<string> onFail)
        {
            string path = Terra_Namp.CachePath;
            string uuid = Guid.NewGuid().ToString();
            var ct = job.CTS.Token;

            try
            {
                // 1. Ensure yt-dlp installed
                job.ResetActivity();
                job.Phase = "Checking yt-dlp...";
                job.Progress = 0.02f;
                await YtDlp.EnsureInstalledAsync(ct);

                // 2. Get video info (title, author, expected size)
                job.ResetActivity();
                job.Phase = "Fetching info...";
                job.Progress = 0.05f;
                var (title, author, expectedBytes, duration) = await GetVideoInfoAsync(job, ct);
                job.Title = title;
                job.ExpectedBytes = expectedBytes;
                job.Duration = duration;

                // 3. Download + convert
                job.ResetActivity();
                job.Phase = "Downloading...";
                job.Progress = 0.10f;
                FFmpeg.EnsureStandardNameExists();
                string mp3Path = Path.Combine(path, $"{uuid}.mp3");
                Terra_Namp.Instance?.Logger.Info($"[DL] Starting download: uuid={uuid}, path={mp3Path}");
                await DownloadAudioAsync(job, mp3Path, uuid, ct);

                bool exists = File.Exists(mp3Path);
                long finalSize = exists ? new FileInfo(mp3Path).Length : 0;
                Terra_Namp.Instance?.Logger.Info($"[DL] Download done: exists={exists}, size={finalSize}");

                if (!exists)
                    throw new FileNotFoundException("yt-dlp did not produce an output file.");

                // 4. Hash + metadata
                job.Phase = "Hashing...";
                job.Progress = 0.95f;

                string titleFile = Path.Combine(path, $"{uuid}.txt");
                byte[] hashBytes = ContentHash.ComputeHash(mp3Path);
                string hashHex = ContentHash.HashToHex(hashBytes);

                File.WriteAllText(titleFile,
                    $"{title}{Environment.NewLine}{author}{Environment.NewLine}{hashHex}{Environment.NewLine}Downloads");

                SongRegistry.Instance?.RegisterSong(uuid, hashHex);

                // Done
                job.Uuid = uuid;
                job.Progress = 1f;
                job.Phase = "Done!";
                job.IsComplete = true;
                job.CompletedAt = DateTime.UtcNow;
                onComplete?.Invoke(uuid);
            }
            catch (OperationCanceledException)
            {
                Cleanup(path, uuid);
                if (!job.IsFailed) // watchdog may have already set this
                {
                    job.IsFailed = true;
                    job.FailMessage = "Cancelled";
                    job.Phase = "Cancelled";
                    job.CompletedAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Cleanup(path, uuid);
                job.IsFailed = true;
                job.FailMessage = ex.Message;
                job.Phase = "Failed";
                job.CompletedAt = DateTime.UtcNow;
                onFail?.Invoke(ex.Message);
            }
        }

        private static async Task<(string title, string author, long expectedBytes, double duration)> GetVideoInfoAsync(DownloadJob job, CancellationToken ct)
        {
            // -x: select audio format so filesize_approx reflects audio size, not video
            string args = $"-j -x --audio-format mp3 --no-playlist \"{job.Url}\"";
            using var process = YtDlp.Run(args);

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            job.ResetActivity();

            string output = await outputTask;

            if (process.ExitCode != 0)
            {
                string error = await errorTask;
                throw new Exception($"yt-dlp info failed: {error}");
            }

            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            string title = root.TryGetProperty("title", out var t) ? t.GetString() : "Unknown";
            string author = root.TryGetProperty("channel", out var c) ? c.GetString()
                          : root.TryGetProperty("uploader", out var u) ? u.GetString()
                          : "";

            // Get expected AUDIO file size (not video — we passed -x above)
            long expectedBytes = 0;
            if (root.TryGetProperty("filesize_approx", out var fsa) && fsa.ValueKind == JsonValueKind.Number)
                expectedBytes = fsa.GetInt64();
            else if (root.TryGetProperty("filesize", out var fs) && fs.ValueKind == JsonValueKind.Number)
                expectedBytes = fs.GetInt64();

            // Fallback: estimate from duration (~192 kbps audio = 24000 bytes/sec)
            double duration = 0;
            if (root.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.Number)
                duration = dur.GetDouble();

            if (expectedBytes <= 0 && duration > 0)
                expectedBytes = (long)(duration * 24000);

            var log = Terra_Namp.Instance?.Logger;
            log?.Info($"[DL] Info: title=\"{title}\", duration={duration:F1}s, expectedBytes={expectedBytes}");

            return (title, author, expectedBytes, duration);
        }

        private static async Task DownloadAudioAsync(DownloadJob job, string outputPath, string uuid, CancellationToken ct)
        {
            string ffmpegDir = Terra_Namp.CachePath;
            string args = $"-x --audio-format mp3 --audio-quality 0 " +
                          $"--postprocessor-args \"ffmpeg:-af loudnorm\" " +
                          $"--ffmpeg-location \"{ffmpegDir}\" " +
                          $"--no-playlist " +
                          $"-o \"{outputPath}\" \"{job.Url}\"";

            using var process = YtDlp.Run(args);

            // Drain both stdout and stderr to prevent pipe deadlock
            _ = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();

            // Linked CTS: cancels monitor when process exits OR when job is cancelled
            using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var monitorToken = monitorCts.Token;

            var log = Terra_Namp.Instance?.Logger;
            log?.Info($"[DL] Monitor start: dir=\"{ffmpegDir}\", pattern=\"{uuid}*\", expectedBytes={job.ExpectedBytes}");

            // Estimated final MP3 size: 320kbps = 40000 bytes/sec
            long estimatedMp3Size = job.Duration > 0 ? (long)(job.Duration * 40000) : 0;

            // File-size monitoring: polls disk every 500ms for real progress
            // Tracks download (.webm) and conversion (.mp3) phases separately
            var monitorTask = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                long lastTrackSize = 0;
                long lastMs = 0;
                int tick = 0;
                bool inConversion = false;

                while (!monitorToken.IsCancellationRequested)
                {
                    try { await Task.Delay(500, monitorToken); }
                    catch (OperationCanceledException) { break; }

                    tick++;

                    // Scan files: separate download source vs output mp3
                    long downloadSize = 0; // .webm, .webm.part, etc. (source audio stream)
                    long mp3Size = 0;      // .mp3 (ffmpeg output)
                    string[] foundFiles = Array.Empty<string>();
                    try
                    {
                        foundFiles = Directory.GetFiles(ffmpegDir, $"{uuid}*");
                        foreach (var f in foundFiles)
                        {
                            try
                            {
                                long fSize = new FileInfo(f).Length;
                                if (f.EndsWith(".mp3"))
                                    mp3Size = fSize;
                                else
                                    downloadSize += fSize;
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Warn($"[DL] Monitor error: {ex.Message}");
                        continue;
                    }

                    long totalSize = downloadSize + mp3Size;
                    if (totalSize <= 0) continue;
                    job.ResetActivity();

                    // Detect conversion phase: mp3 file exists
                    if (mp3Size > 0 && !inConversion)
                    {
                        inConversion = true;
                        lastTrackSize = 0;
                        lastMs = 0;
                        log?.Info($"[DL] Conversion phase started: mp3Size={mp3Size}, downloadSize={downloadSize}");
                    }

                    // Log every 4th tick (~2s)
                    if (tick % 4 == 0)
                    {
                        var fileNames = string.Join(", ", foundFiles.Select(Path.GetFileName));
                        log?.Info($"[DL] Monitor tick={tick}: files=[{fileNames}], dl={downloadSize}, mp3={mp3Size}, expected={job.ExpectedBytes}, estMp3={estimatedMp3Size}, progress={job.Progress:F3}, conv={inConversion}");
                    }

                    // Track the relevant size for speed calculation
                    long trackSize = inConversion ? mp3Size : downloadSize;

                    // Calculate speed
                    long nowMs = sw.ElapsedMilliseconds;
                    if (trackSize > lastTrackSize && lastMs > 0 && nowMs > lastMs)
                    {
                        double speed = (trackSize - lastTrackSize) / ((nowMs - lastMs) / 1000.0);
                        job.Speed = FormatBytes(speed) + "/s";

                        long expected = inConversion ? estimatedMp3Size : job.ExpectedBytes;
                        if (expected > 0 && speed > 0)
                        {
                            long remaining = Math.Max(0, expected - trackSize);
                            int etaSecs = (int)(remaining / speed);
                            job.ETA = etaSecs > 0 ? FormatTime(etaSecs) : null;
                        }
                    }
                    lastTrackSize = trackSize;
                    lastMs = nowMs;

                    // Update progress
                    if (inConversion && estimatedMp3Size > 0)
                    {
                        // Conversion phase: 80% - 95%
                        float pct = Math.Min((float)mp3Size / estimatedMp3Size, 1f);
                        float mapped = 0.80f + pct * 0.15f;
                        if (mapped > job.Progress)
                        {
                            job.Progress = mapped;
                            job.Phase = "Converting...";
                        }
                    }
                    else if (!inConversion && job.ExpectedBytes > 0)
                    {
                        // Download phase: 10% - 80%
                        float pct = Math.Min((float)downloadSize / job.ExpectedBytes, 1f);
                        float mapped = 0.10f + pct * 0.70f;
                        if (mapped > job.Progress)
                        {
                            job.Progress = mapped;
                            job.Phase = "Downloading...";
                        }
                    }
                    else if (job.Progress < 0.15f)
                    {
                        job.Progress = 0.15f;
                        job.Phase = "Downloading...";
                    }
                }

                log?.Info($"[DL] Monitor stopped: tick={tick}, progress={job.Progress:F3}");
            }, monitorToken);

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            finally
            {
                // Cancel monitor so we don't deadlock on await
                monitorCts.Cancel();
            }

            try { await monitorTask; }
            catch (OperationCanceledException) { }

            log?.Info($"[DL] yt-dlp exited with code {process.ExitCode}");

            if (process.ExitCode != 0)
                throw new Exception($"yt-dlp download failed (exit code {process.ExitCode})");
        }

        private static string FormatBytes(double bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024 * 1024):F1} MiB";
            if (bytes >= 1024)
                return $"{bytes / 1024:F0} KiB";
            return $"{bytes:F0} B";
        }

        private static string FormatTime(int totalSeconds)
        {
            if (totalSeconds >= 3600)
                return $"{totalSeconds / 3600}:{totalSeconds % 3600 / 60:D2}:{totalSeconds % 60:D2}";
            return $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
        }

        private static void Cleanup(string path, string uuid)
        {
            // Retry a few times — yt-dlp process may take a moment to release file handles after kill
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    File.Delete(Path.Combine(path, $"{uuid}.mp3"));
                    File.Delete(Path.Combine(path, $"{uuid}.txt"));
                    foreach (string tmp in Directory.GetFiles(path, $"{uuid}.*"))
                        File.Delete(tmp);
                    return; // success
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(500); // wait for process to release handles
                }
                catch { return; }
            }
        }
    }
}
