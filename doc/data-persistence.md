# Data Persistence

## PersistentDataStore System

```
PersistentDataStore (abstract, ILoadable)
  FileName -> Load() via TagIO -> ForceSave() via TagIO.ToFile()

PersistentDataStoreSystem (static)
  GetDataStore<T>() -> T
```

## TerraDataStore

**File:** `MP3PlayerCache/playback_preferences.dat` (TagIO binary)

| Tag Key | Type | Default |
|---------|------|---------|
| `Terra_Namp:PlayMode` | int | 0 (Once) |
| `Terra_Namp:VolumeLevel` | float | 0.5 |
| `Terra_Namp:WindowPosX/Y` | float | 0.65 / 0.5 |
| `Terra_Namp:PanelOpacity` | float | 0.55 |
| `Terra_Namp:BlurLevel` | int | 5 |
| `Terra_Namp:CornerRadius` | int | 6 |
| `Terra_Namp:PanelColor[RGB]` | byte*3 | (0,255,255) cyan |
| `Terra_Namp:SecondaryColor[RGB]` | byte*3 | (226,114,175) pink |
| `Terra_Namp:PanelBg[RGB]` | byte*3 | (0,0,0) black |

**Legacy:** Reads old `MP3Player:VolumeAngle` (radians -> 0-1).

## Two Persistence Systems

| | ModConfig | PersistentDataStore |
|---|---|---|
| File | `ModConfigs/*.json` | `MP3PlayerCache/*.dat` |
| Format | JSON (tModLoader) | TagIO (binary) |
| GUI | Mods -> Config | Settings tab in player UI |
| Settings | `SendNowPlayingMessages`, `EnablePrefetch` | All UI prefs + playback state |

## What is Synced (Network)

**Synced:** current song (hash), play/pause/stop, seek position, metadata, forced flag.

**NOT synced (client-only):** volume, window position, UI colors, visual settings, play mode.

**Volume flow:** `volumeSlider.Volume` -> `SetVolume(v)` -> `v^3 * VolumeFadeMultiplier` -> `audioTrack.Volume`

## Save Triggers

`ForceSave()` called on: volume slider release, play mode change, panel drag end, settings change, `PreSaveAndQuit()`, `Unload()` (dev mode recompile fix).

## Cache Directory

```
{Main.SavePath}/MP3PlayerCache/
  ffmpeg*, yt-dlp*, qjs*             Platform binaries
  quickjs.ver                        QuickJS version tracking
  playback_preferences.dat           Settings (TagIO)
  {uuid}.mp3 + {uuid}.txt            Client songs
  {hashHex}.mp3 + {hashHex}.txt      Server cached songs
```
