# Audio System

## DynamicMP3AudioTrack

Core audio engine extending Terraria's `ASoundEffectBasedAudioTrack`:

```
Constructor(Stream) -> MP3Stream -> CreateSoundEffect(frequency, Stereo)
ReadAheadPutAChunkIntoTheBuffer() -> reads mp3Stream -> submits to SoundEffectInstance

Properties:
  Progress     = mp3Stream.Position / mp3Stream.Length (0.0 - 1.0)
  ElapsedTime  = SongDuration * Progress
  SongDuration = mp3Stream.Length / 40000 (seconds)
  BufferToSubmit = raw PCM byte[] (used by Visualizer)
```

**Format:** All files encoded via FFmpeg to 320kbps MP3 at 40kHz stereo. Constant `Bitrate = 40000`.

## PlaybackController

Wraps `DynamicMP3AudioTrack` with playlist/network logic:

- **Playback:** `Toggle()`, `Skip(seconds)`, `SeekToProgress()` -- broadcast to network
- **Network receive:** `PauseFromNetwork()`, `ResumeFromNetwork()`, `SeekFromNetwork()` -- no broadcast
- **Auto-advance:** `UpdateAudioTrack()` handles play modes (Once/Loop/Autoplay/Shuffle)
- **Playlist:** `GetNextSongUuid()`, `GetPreviousSongUuid()`, `GetRandomSong()`, `GetSortedSongList()`
- **Prefetch:** `GetUpcomingSongUuids(count)` returns next N songs based on play mode
- **Volume:** `SetVolume(v)` applies cubic curve: `v³ * VolumeFadeMultiplier`

## TerraSceneEffect

```csharp
ModSceneEffect:
  Music = Silence track
  IsSceneEffectActive = ActiveSong != null
  Priority = BossHigh (overrides everything)
```

Replaces ALL vanilla music with silence when a song is playing. Actual audio from `DynamicMP3AudioTrack`.

## TerraTrackUpdaterSystem

Runs every tick via `PostUpdateInput()`:
- Resets `CurrentlyForcingSong`
- `HandleFade()`: forced song volume sync + 3-second fade-out on end
- `UpdateActiveSong()` -> `PlaybackController.UpdateAudioTrack()`
- `PreSaveAndQuit()` / `Unload()` -> force-saves `TerraDataStore`

**Forced song lifecycle:** Network sets `CurrentlyForcingSong=true` -> loops track -> condition ends -> 3s fade-out (1/180 per frame) -> stops.

## AsyncMP3Downloader (YouTube)

```
DownloadFromUrl(url, onProgress, onComplete, onFail)
  Task.Run:
    1. YtDlp.EnsureInstalledAsync()        (0.1)
    2. yt-dlp --print title --print channel (0.2)
    3. yt-dlp -x --audio-format mp3 -af loudnorm (0.4)
    4. Compute MD5 hash                     (0.8)
    5. Write metadata .txt                  (0.9)
    6. Register in SongRegistry             (1.0)
  180-second watchdog via CancellationToken
```

## Local File Import (AddTracksPanel)

- **Browse:** Multi-file dialog (mp3, flac, wav, ogg, m4a, wma, aac)
- **Import Folder:** Selects any file -> imports ALL audio from that directory
- Pipeline: FFmpeg `-af loudnorm` to 320kbps -> MD5 hash -> metadata .txt -> SongRegistry
- Sequential queue: one file at a time, skips duplicates via hash check

## YtDlp

Auto-downloads yt-dlp + QuickJS (required for YouTube since yt-dlp 2025.11+):
- Downloads platform-specific binaries from GitHub/bellard.org
- `Run(args)` auto-injects `--js-runtimes "quickjs:{path}"`
- Version tracking via `quickjs.ver` (prevents slow QuickJS-NG)

**Binaries:** yt-dlp (Win/Linux/macOS), QuickJS from bellard.org (NOT QuickJS-NG).
