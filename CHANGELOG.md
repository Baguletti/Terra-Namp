# Changelog

## v1.1.3

### Text Input
- All text fields now support cursor positioning with arrow keys, Home/End, Delete
- Click to place cursor at any position in the text
- Key repeat when holding arrow keys (0.3s delay, then fast scroll)
- Emoji-safe cursor navigation (surrogate pairs handled correctly)

### Soundpad
- Fixed garbled unicode tooltip when hovering sound pads
- Fixed crash when deleting a sound that is currently playing

### Playlist Download
- Download entire YouTube playlists by pasting the playlist URL
- Tracks from a playlist are automatically grouped into a folder named after the playlist
- Progress bar shows overall playlist completion (% and ETA for the whole playlist, not per track)
- Scrolling title displays the playlist name
- Multiple playlists can be downloaded simultaneously
- Duplicate tracks are automatically skipped (MD5 dedup)
- Resilient to per-track errors: skips failed tracks and continues (aborts after 5 consecutive failures)

### URL Detection
- Explicit playlist URLs (`youtube.com/playlist?list=...`, `music.youtube.com/playlist?list=...`) trigger playlist mode
- Video URLs (including those with `list=` parameter) still download a single track as before

## v1.0

- Initial release
- YouTube single-track download via yt-dlp
- Local file import (Browse / Import Folder)
- Folder-based song organization
- Multiplayer sync with chunk-based transfer and prefetch
- Soundpad, visualizer, slowed + reverb effect
- Customizable UI with drag, theme colors, NowPlaying popup
