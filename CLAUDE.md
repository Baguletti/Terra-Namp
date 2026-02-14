# Terra Namp Mod

**Platform:** Terraria tModLoader (.NET 8.0) | **Side:** Both | **Author:** Baguletti

In-game music player: YouTube download (yt-dlp), local file import, multiplayer sync via chunk-based transfer. DJ controls playback, all clients hear the same song.

## Directory Structure

```
src/Terra_Namp/
|-- Terra_Namp.cs                  Entry point
|-- Terra_NampConfig.cs            ModConfig (NowPlaying, Prefetch toggles)
|-- FFmpeg.cs / YtDlp.cs           External binary management
|
|-- Common/
|   |-- Players/TerraModPlayer.cs  Keybind, OnEnterWorld sync
|   +-- UI/Abstract/
|       |-- SmartUIState.cs        Auto-loaded UIState base
|       |-- SmartUIElement.cs      UIElement with Safe* wrappers
|       |-- DraggableUIElement.cs  Draggable panel base
|       |-- ScrollablePanel.cs     Scrollable container base
|       +-- PressAnimator.cs       Button press animation module
|
|-- Content/
|   |-- Audio/
|   |   |-- TerraSceneEffect.cs    Replaces vanilla music with silence
|   |   +-- TerraTrackUpdaterSystem.cs  Tick-based fade/update logic
|   |-- IO/TerraDataStore.cs       Persistent preferences (TagIO)
|   +-- UI/TerraUI/
|       |-- TerraMainPanel.cs      Main draggable panel (340x520)
|       |-- TerraState.cs          SmartUIState wrapper
|       |-- PlaybackController.cs  Per-song controller
|       |-- TextBanner.cs          Scrolling text
|       +-- Components/            UI components (buttons, seekbar, visualizer, etc.)
|
|-- Core/
|   |-- Audio/
|   |   |-- DynamicMP3AudioTrack.cs  MP3Sharp streaming
|   |   +-- AsyncMP3Downloader.cs    YouTube download pipeline
|   |-- IO/                        PersistentDataStore, MultiNativeFileDialog
|   |-- Services/                  SongCacheService, FileImportService
|   +-- UI/TerraUILoader.cs        Reflection-based UI auto-loader
|
|-- Networking/
|   |-- PacketType.cs              Enum (Play/Stop/Pause/Resume/Seek/Transfer/Sync/Prefetch)
|   |-- PacketRouter.cs            Switch-based dispatch
|   |-- PacketBuilder.cs           Static packet factory
|   |-- SongRegistry.cs            Hash <-> UUID mapping
|   |-- SongTransferManager.cs     Chunk transfer + prefetch queue
|   |-- ServerJukeboxState.cs      Server-side playback state
|   +-- Handlers/                  One handler per packet type
|
+-- Localization/                  en-US hjson + LocalizationHelper
```

## Architecture Overview

### UI System
`TerraUILoader` auto-discovers `SmartUIState` subclasses via reflection, creates instances with paired `UserInterface`. Main panel is `DraggableUIElement` with tab-based layout (Player/Add/Settings). All buttons use `PressAnimator` for press animation. Text input handled in `Draw()` phase.

### Audio System
`DynamicMP3AudioTrack` (MP3Sharp) streams PCM to `SoundEffectInstance`. `TerraSceneEffect` overrides vanilla music at `BossHigh` priority. `PlaybackController` wraps audio track with playlist logic and network broadcasting.

### Networking
Songs identified by MD5 hash (network) / UUID (local). Chunk-based transfer (8KB * 4/tick). Server relays chunks from DJ to clients, caches files. All handlers: server rebroadcasts, client applies via `Main.QueueMainThreadAction()`. Prefetch system syncs upcoming 10 songs in background.

### Data Persistence
Two systems: `ModConfig` (JSON, tModLoader standard) for toggles, `PersistentDataStore` (TagIO binary) for UI preferences. Nothing synced except playback state. Volume applied client-side with cubic curve.

## Detailed Documentation

- [UI Architecture](doc/ui-architecture.md) — class hierarchy, components, drawing patterns, NowPlaying popup
- [UI Rendering](doc/ui-rendering.md) — SpriteBatch rules, scissor clipping pattern, common mistakes
- [Networking](doc/networking.md) — packets, transfer flow, handler pattern, prefetch, packet formats
- [Audio](doc/audio.md) — MP3 streaming, playback controller, scene effect, download pipeline, yt-dlp
- [Data Persistence](doc/data-persistence.md) — data stores, cache structure, sync behavior, save triggers
- [Configuration](doc/configuration.md) — build.txt, ModConfig, keybinds, theme system
- [Shaders](doc/shaders.md) — когда использовать шейдеры, компиляция .xnb
- [Window Bounds](doc/window-bounds.md) — anti-clipping, clamping, offscreen hiding, mode switching
