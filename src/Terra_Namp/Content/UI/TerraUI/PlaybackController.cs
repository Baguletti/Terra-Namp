using Microsoft.Xna.Framework.Audio;
using Terra_Namp.Content.Audio;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Enums;
using Terra_Namp.Core.Audio;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.Services;
using Terra_Namp.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp.Content.UI.TerraUI;

public class PlaybackController : IDisposable
{
    private readonly DynamicAudioTrack audioTrack;
    private readonly TerraMainPanel panel;

    public string Uuid { get; }
    public string Name { get; }
    public string Author { get; }
    public bool Forced { get; }
    public float VolumeFadeMultiplier { get; set; } = 1f;

    public byte[] BufferToSubmit => audioTrack?.BufferToSubmit ?? Array.Empty<byte>();
    public float Progress => audioTrack?.Progress ?? 0f;
    public TimeSpan ElapsedTime => audioTrack?.ElapsedTime ?? TimeSpan.Zero;
    public TimeSpan SongDuration => audioTrack?.SongDuration ?? TimeSpan.Zero;
    public bool IsPlaying => audioTrack?.IsPlaying ?? false;
    public bool IsPaused => audioTrack?.IsPaused ?? false;
    public bool IsStopped => audioTrack == null || audioTrack.IsStopped;

    public bool SlowedReverbEnabled
    {
        get => audioTrack?.SlowedReverbEnabled ?? false;
        set { if (audioTrack != null) audioTrack.SlowedReverbEnabled = value; }
    }

    public bool Failed { get; private set; }

    public PlaybackController(string uuid, TerraMainPanel panel, bool forced)
    {
        Uuid = uuid;
        this.panel = panel;
        Forced = forced;

        try
        {
            string titlePath = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");
            if (File.Exists(titlePath))
            {
                string[] text = File.ReadAllLines(titlePath);
                Name = text.Length >= 1 ? text[0] : "Unknown";
                Author = text.Length >= 2 ? text[1] : "";
            }
            else
            {
                Name = "Unknown";
                Author = "";
            }

            string songPath = Path.Combine(Terra_Namp.CachePath, $"{uuid}.mp3");
            if (!File.Exists(songPath))
            {
                Terra_Namp.Instance?.Logger.Error($"Song file not found: {songPath}");
                Failed = true;
                return;
            }

            audioTrack = new DynamicAudioTrack(new FileStream(songPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536));
            Toggle();
        }
        catch (Exception ex)
        {
            Terra_Namp.Instance?.Logger.Error($"Failed to create PlaybackController for {uuid}: {ex.Message}");
            Failed = true;
        }
    }

    public void UpdateAudioTrack()
    {
        if (audioTrack == null) return;
        audioTrack.Update();
        audioTrack.UpdateVolumeFade(1f / 60f); // Assume 60fps

        if (audioTrack.IsStopped)
        {
            if (ModContent.GetInstance<TerraTrackUpdaterSystem>().CurrentlyForcingSong)
            {
                audioTrack.Reuse();
                audioTrack.PlaySmooth();
                return;
            }

            var playMode = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PlayMode;
            switch (playMode)
            {
                case PlayMode.Next:
                    panel.StopCurrentSong();
                    panel.BeginPlayingSong(GetNextSong(1));
                    break;
                case PlayMode.Shuffle:
                    panel.StopCurrentSong();
                    panel.BeginPlayingSong(GetRandomSong());
                    break;
                case PlayMode.Loop:
                    audioTrack.Reuse();
                    audioTrack.PlaySmooth();
                    break;
            }
        }
    }

    public void SetVolume(float volume)
    {
        if (audioTrack == null) return;

        var system = ModContent.GetInstance<TerraTrackUpdaterSystem>();
        if (!system.CurrentlyFadingOut && !system.CurrentlyForcingSong)
            VolumeFadeMultiplier = 1;

        // Cubic curve: maps linear slider 0-1 to perceptual volume.
        // Gives much finer control at low volumes where human hearing is most sensitive.
        // 10% slider → 0.1% volume, 50% → 12.5%, 100% → 100%
        float curved = volume * volume * volume;

        float finalVolume = curved * VolumeFadeMultiplier;
        audioTrack.Volume = finalVolume;
    }

    public void Toggle()
    {
        // Client-side permission check
        if (Main.netMode == NetmodeID.MultiplayerClient
            && !ClientPermissionCache.GetLocalPermissions().CanPlay)
            return;

        if (audioTrack.IsPlaying)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetLogger.Info($"Toggle: pausing \"{Name}\" -> sending PauseSong");
                PacketBuilder.PauseSong((byte)Main.myPlayer).Send();
            }
            audioTrack.PauseSmooth();
        }
        else if (audioTrack.IsStopped)
        {
            audioTrack.PlaySmooth();
        }
        else if (audioTrack.IsPaused)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetLogger.Info($"Toggle: resuming \"{Name}\" -> sending ResumeSong");
                PacketBuilder.ResumeSong((byte)Main.myPlayer).Send();
            }
            audioTrack.ResumeSmooth();
        }
    }

    public void Skip(double seconds)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient
            && !ClientPermissionCache.GetLocalPermissions().CanPlay)
            return;

        audioTrack.Skip(seconds);

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Info($"Skip: {seconds:+0;-0}s -> sending SeekPosition progress={audioTrack.Progress:F3}");
            PacketBuilder.SeekPosition((byte)Main.myPlayer, audioTrack.Progress).Send();
        }
    }

    public void SeekToProgress(float progress)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient
            && !ClientPermissionCache.GetLocalPermissions().CanPlay)
            return;

        audioTrack.SeekToProgress(progress);

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Info($"SeekToProgress: {progress:F3} -> sending SeekPosition");
            PacketBuilder.SeekPosition((byte)Main.myPlayer, progress).Send();
        }
    }

    public void PauseFromNetwork()
    {
        NetLogger.Info($"PauseFromNetwork: \"{Name}\"");
        if (audioTrack.IsPlaying)
            audioTrack.PauseSmooth();
    }

    public void ResumeFromNetwork()
    {
        NetLogger.Info($"ResumeFromNetwork: \"{Name}\"");
        if (audioTrack.IsPaused)
            audioTrack.ResumeSmooth();
    }

    /// <summary>
    /// Force-resumes playback, cancelling any in-progress fade.
    /// Used by soundpad to guarantee main track resumes even if mid-fade-out.
    /// </summary>
    public void ForceResume()
    {
        audioTrack.ForceResume();
    }

    public void SeekFromNetwork(float progress)
    {
        NetLogger.Info($"SeekFromNetwork: \"{Name}\" progress={progress:F3}");
        audioTrack.SeekToProgress(progress);
    }

    /// <summary>
    /// Seeks to position and immediately pauses — no audio starts, no fade race.
    /// Use when restoring a track that was paused before an event (boss/death).
    /// </summary>
    public void SeekAndPauseFromNetwork(float progress)
    {
        NetLogger.Info($"SeekAndPauseFromNetwork: \"{Name}\" progress={progress:F3}");
        audioTrack.SeekAndPause(progress);
    }

    public void ApplySlowedReverbFromNetwork(bool enabled)
    {
        NetLogger.Info($"ApplySlowedReverbFromNetwork: \"{Name}\" enabled={enabled}");
        SlowedReverbEnabled = enabled;
    }

    public void ToggleSlowedReverb()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient
            && !ClientPermissionCache.GetLocalPermissions().CanPlay)
            return;

        SlowedReverbEnabled = !SlowedReverbEnabled;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            PacketBuilder.SlowedReverb((byte)Main.myPlayer, SlowedReverbEnabled).Send();
    }

    public void Dispose()
    {
        if (audioTrack == null) return;
        try
        {
            audioTrack.Stop(AudioStopOptions.Immediate);
            audioTrack.Dispose();
        }
        catch (Exception ex)
        {
            Terra_Namp.Instance?.Logger.Error($"PlaybackController.Dispose error: {ex.Message}");
        }
    }

    public string GetNextSongUuid() => GetNextSong(1);
    public string GetPreviousSongUuid() => GetNextSong(-1);

    /// <summary>
    /// Returns UUIDs of upcoming songs for prefetch based on current play mode.
    /// </summary>
    public List<string> GetUpcomingSongUuids(int count)
    {
        var playMode = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PlayMode;
        if (playMode == PlayMode.Loop)
            return new List<string>();

        var songs = GetSortedSongList();
        if (songs.Count <= 1)
            return new List<string>();

        var result = new List<string>();
        int currentIndex = songs.FindIndex(s => s.Uuid == Uuid);

        if (playMode == PlayMode.Shuffle)
        {
            // Pick random songs (excluding current).
            var indices = new HashSet<int> { currentIndex };
            int toGet = Math.Min(count, songs.Count - 1);
            while (result.Count < toGet)
            {
                int idx = Main.rand.Next(songs.Count);
                if (indices.Add(idx))
                    result.Add(songs[idx].Uuid);
            }
        }
        else
        {
            // Next/Autoplay: sequential from current position.
            for (int i = 1; i <= Math.Min(count, songs.Count - 1); i++)
            {
                int idx = (currentIndex + i) % songs.Count;
                result.Add(songs[idx].Uuid);
            }
        }

        return result;
    }

    private List<(string Title, string Uuid)> GetSortedSongList()
    {
        var (songs, _) = SongCacheService.GetSongsAndFolders(panel.FolderFilter);
        return songs;
    }

    private string GetNextSong(int step)
    {
        var songs = GetSortedSongList();
        int index = songs.FindIndex(s => s.Uuid == Uuid);
        index += step;
        if (index < 0) index = songs.Count - 1;
        if (index > songs.Count - 1) index = 0;
        return songs[index].Uuid;
    }

    private string GetRandomSong()
    {
        var songs = GetSortedSongList();
        if (songs.Count == 1) return songs[0].Uuid;

        int randomIndex = Main.rand.Next(songs.Count);
        while (randomIndex == songs.FindIndex(s => s.Uuid == Uuid))
            randomIndex = Main.rand.Next(songs.Count);

        return songs[randomIndex].Uuid;
    }
}
