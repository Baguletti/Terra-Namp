# Terra Namp — Feature Overview

In-game music player for Terraria (tModLoader). YouTube downloads, local file import, multiplayer sync, soundpad, slowed+reverb effect, full UI customization.

## Music Player

- Full MP3 streaming via MP3Sharp decoder (40 kHz, stereo 16-bit PCM)
- Replaces vanilla Terraria music at BossHigh priority when active
- Controls: Previous / Rewind (-10s) / Play-Pause / Forward (+10s) / Next
- Play modes: Sequential, Shuffle, Loop (persisted between sessions)
- Seek bar with elapsed/total time display
- Real-time audio visualizer:
  - **Bars mode** — 48 bars with accent-to-secondary gradient and mirrored reflection
  - **Smooth Wave mode** — 64 sample points, Catmull-Rom spline interpolation, anti-aliased 2-3px line with perspective reflection
- Smooth fade-in/fade-out (0.3s cosine S-curve) on play/pause — no clicks
- PCM micro-fade (10ms) at track start and end to eliminate decoder artifacts

## YouTube Downloads

- Downloads via yt-dlp with automatic binary management (Windows/Linux/macOS)
- FFmpeg post-processing: loudnorm normalization, 320 kbps MP3 output
- Parallel downloads — unlimited concurrent jobs with independent progress
- Real progress tracking via file-size monitoring (polls disk every 500ms):
  - Download phase (10%–80%): tracks source file (.webm/.part) vs expected audio size
  - Conversion phase (80%–95%): tracks .mp3 output vs estimated final size
  - Real-time speed (KiB/s, MiB/s) and ETA countdown
- Auto-extracts title and author from video metadata
- 300-second inactivity timeout with auto-cancellation
- Completed jobs display for 5 seconds then auto-remove
- Progress bars with scrolling title marquee in the UI

## Local File Import

- Supported formats: MP3, FLAC, WAV, OGG, M4A, WMA, AAC
- Single file browse or batch folder import
- Sequential queue processing (one file at a time to avoid UI freezing)
- FFmpeg conversion with loudnorm normalization to 320 kbps MP3
- Automatic folder tagging based on source directory name
- MD5 hash deduplication — duplicates are detected and skipped
- Progress counter: `[3/12] filename...`

## Slowed + Reverb Effect

- Toggle button synced across multiplayer (all clients hear the same effect)
- Pitch: -0.25 semitones (2^-0.25 = ~84% playback speed)
- Freeverb-style spatial reverb (in-place PCM processing):
  - **Pre-delay**: 25ms (1000 samples) — separates dry vocal attack from reverb onset
  - **Biquad high-pass filter**: 200Hz Butterworth cutoff on reverb input — removes low-frequency muddiness, bass stays in dry signal only
  - **8 parallel lowpass-feedback comb filters** (Jezar delays scaled to 40kHz, +23 sample stereo spread)
  - **3 series allpass diffusers** for diffuse tail
  - Feedback: 0.82, Damping: 0.35, Dry mix: 85%, Wet mix: 23.4%
- Reset on toggle-off to clear all buffers instantly

## Soundpad

- 5x5 button grid (25 sounds per page) with pagination
- Drag-and-drop emoji picker (scrollable, 6 rows visible)
- Custom naming for each pad
- Independent volume slider with cubic curve
- Auto-pauses main music during soundpad playback, auto-resumes on finish
- Press animations on all buttons
- Separate keybind (L) to open standalone soundpad popup

## Playlist Management

- Folder-based organization: "Downloads", "Singles", custom folder names from imports
- Folder filter cycling button ("All" → "Downloads" → "Singles" → custom...)
- Real-time search filtering by song name
- Song deletion (right-click, Add tab only — disabled on Player tab)
- Active song highlighting with accent color
- Backward-compatible migration of legacy track metadata

## Multiplayer & Networking

### DJ System
- One DJ per session — the player who starts playback
- All clients hear the same song synchronized by the server
- Server tracks: current song hash, title, author, DJ index, playback progress, paused duration, slowed-reverb state

### Chunk-Based Song Transfer
- Songs identified by MD5 hash (network) / UUID (local)
- Chunk size: 8 KB, transfer rate: 4 chunks/tick (32 KB/tick at 60fps = ~1.9 MB/s)
- Flow: SongHeader (metadata) → SongChunk (sequential data) → SongTransferComplete (MD5 validation)
- Server caches transferred songs for future clients

### Prefetch System
- When DJ starts playback, broadcasts entire song library to all clients
- Clients check which songs they're missing and download them in the background
- Max 2 concurrent prefetch transfers to avoid saturating the connection
- Configurable via ModConfig toggle (enabled by default)

### Permissions
- Three roles: **Listener** (view only), **Controller** (play/pause/seek/effects), **Admin** (full control + permission management)
- Auto-admin for first player in host+play mode
- Super user system for dedicated servers (`/terra-namp-admin <player>` console command)
- Admin panel: player list with role indicators, per-player access/admin toggles

### Packet Types
Play (1), Stop (2), Pause (3), Resume (4), SeekPosition (5), SlowedReverb (6), RequestSong (10), SongHeader (11), SongChunk (12), SongTransferComplete (13), SyncState (20), RequestState (21), PrefetchList (30), PermissionUpdate (40), PermissionSync (41)

## UI Customization

- Draggable panel (340x520) with persistent position
- Mini Player mode (340x96) — compact view with visualizer and controls
- **Panel opacity**: 10%–100% (default 60%)
- **Blur level**: 0–10 (default 10, SilkyUI integration)
- **Corner radius**: 0–12px (default 6px)
- **Accent color**: primary UI color (default #CC7834)
- **Secondary color**: visualizer gradient (default #8C00EB)
- **Background color**: panel fill (default black)
- Color pickers with hue/saturation wheel and brightness slider
- Now Playing popup: slide-in toast notification with title and author (duration scales with name length)
- Scrolling text (TextBanner) for long titles everywhere: song list, now playing, progress bars

## Volume Control

- Main volume slider with cubic curve scaling (sliderValue^3 for perceptual accuracy)
- Display: 10% slider = 0.1% volume, 50% = 12.5%, 100% = 100%
- Optional game volume override:
  - Sound volume slider (affects Main.soundVolume)
  - Ambient volume slider (requires TerrariaAmbience mod for dynamic control)
  - Original values restored on mod unload

## Keybinds

| Key | Action |
|-----|--------|
| K | Open/Close Music Player |
| L | Open/Close Soundpad |
| (configurable) | Volume Up |
| (configurable) | Volume Down |

## Data Persistence

- **TerraDataStore** (TagIO binary): play mode, volume, window position, all visual settings, colors, visualizer type, mini player state, volume override settings
- **SoundpadDataStore** (TagIO binary): pad names, sound assignments, soundpad volume
- **ModConfig** (JSON): SendNowPlayingMessages, EnablePrefetch toggles
- **Song metadata** (.txt per track): title, author, MD5 hash, folder name

## Platform Support

- Windows, Linux, macOS
- Platform-specific binaries for FFmpeg and yt-dlp (auto-extracted from .gz archives)
- Cross-platform file dialogs via MultiNativeFileDialog

## Dependencies

- **SilkyUIFramework** — required (UI framework, blur rendering)
- **TerrariaAmbienceAPI / TerrariaAmbience** — weak reference (ambient volume control)
- **BetterRussian** — weak reference (a Cyrillic font similar to Vanilla)
- tModLoader .NET 8.0
