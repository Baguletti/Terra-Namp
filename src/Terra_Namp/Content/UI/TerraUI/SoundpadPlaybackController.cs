using Microsoft.Xna.Framework.Audio;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.Audio;
using Terra_Namp.Core.IO;
using System;
using System.IO;

namespace Terra_Namp.Content.UI.TerraUI;

public class SoundpadPlaybackController : IDisposable
{
    private DynamicAudioTrack audioTrack;
    private readonly TerraMainPanel mainPanel;
    private bool pausedMainMusic;

    public bool IsPlaying => audioTrack != null && audioTrack.IsPlaying;
    public bool IsFinished => audioTrack != null && audioTrack.IsStopped;

    public SoundpadPlaybackController(TerraMainPanel mainPanel)
    {
        this.mainPanel = mainPanel;
    }

    public void PlaySound(string uuid)
    {
        // Stop current sound only (don't resume main music yet)
        StopCurrentSound();

        string filePath = Path.Combine(SoundpadDataStore.SoundpadCachePath, $"{uuid}.mp3");
        if (!File.Exists(filePath))
            return;

        // Pause main music only if not already paused by us
        if (!pausedMainMusic && mainPanel?.ActiveSong != null && mainPanel.ActiveSong.IsPlaying)
        {
            mainPanel.ActiveSong.PauseFromNetwork();
            pausedMainMusic = true;
        }

        audioTrack = new DynamicAudioTrack(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
        float volume = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>().VolumeLevel;
        float curved = volume * volume * volume;
        audioTrack.Volume = curved;
        audioTrack.Play();
    }

    private void StopCurrentSound()
    {
        if (audioTrack != null)
        {
            audioTrack.Stop(AudioStopOptions.Immediate);
            audioTrack.Dispose();
            audioTrack = null;
        }
    }

    public void Update()
    {
        if (audioTrack == null)
        {
            // Safety: if no audioTrack but main music was paused by us, force-resume it
            // (covers both mid-fade and already-paused states)
            if (pausedMainMusic && mainPanel?.ActiveSong != null)
            {
                mainPanel.ActiveSong.ForceResume();
                pausedMainMusic = false;
            }
            return;
        }

        // Update volume in real-time
        float volume = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>().VolumeLevel;
        // Cubic curve: maps linear volume 0-1 to perceptual volume (same as main player)
        float curved = volume * volume * volume;
        audioTrack.Volume = curved; // Direct assignment to custom Volume property for true silence at 0%

        audioTrack.Update();

        if (audioTrack.IsStopped)
        {
            Stop();
        }
    }

    public void Stop()
    {
        if (audioTrack != null)
        {
            audioTrack.Stop(AudioStopOptions.Immediate);
            audioTrack.Dispose();
            audioTrack = null;
        }

        if (pausedMainMusic && mainPanel?.ActiveSong != null)
        {
            mainPanel.ActiveSong.ForceResume();
            pausedMainMusic = false;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
