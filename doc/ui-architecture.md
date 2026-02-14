# UI Architecture

## Auto-Loading System

```
TerraUILoader (ModSystem)
    |
    | PostSetupContent() - scans assembly for SmartUIState subclasses
    | Creates instances via reflection + pairs with UserInterface
    |
    +-- SmartUIState instances:
        |-- TerraState (main player panel)
        +-- NowPlayingState (popup notifications)
```

`TerraUILoader` uses reflection to find all non-abstract `SmartUIState` subclasses. For each, it creates an instance and a paired `UserInterface`:
- `UpdateUI()` -- updates all visible UI states each frame
- `ModifyInterfaceLayers()` -- inserts each state at its `InsertionIndex`
- `GetUIState<T>()` -- retrieves the singleton instance
- `ReloadState<T>()` -- force-recreates a state and its UserInterface

**Guard:** Entire UI system is skipped on dedicated servers (`Main.dedServ`).

## Class Hierarchy

```
UIState (Terraria)
  +-- SmartUIState (abstract)
      |   Visible, InsertionIndex(), Safe* wrappers, AddElement()
      |-- TerraState          InsertionIndex = "Vanilla: Mouse Text"
      +-- NowPlayingState     InsertionIndex = layers.Count - 1 (topmost)

UIElement (Terraria)
  +-- SmartUIElement (abstract)
  |     Safe* wrappers for all mouse/update/scroll events
  |
  |-- DraggableUIElement (abstract)
  |     DragBox, DefaultPosition, drag tracking, off-screen reset
  |     Position applied in Draw() via needsRecalcNextDraw flag
  |     +-- TerraMainPanel (340x520)
  |
  |-- ScrollablePanel (abstract)
  |     Scroll offset, scrollbar dragging, GridSnap, scissor clipping
  |     +-- ScrollableSongList
  |     +-- AdminPanel (player permissions list)
  |
  |-- PressAnimator (composition module)
  |     Smooth scale animation for any button (lerp in Draw phase)
  |
  |-- IconButton, PlayPauseButton, IconToggleButton, ControlButton
  |-- SeekBar, Visualizer, VolumeSlider, SearchField
  |-- YoutubeLinkField, NowPlayingPopup, NowPlayingWidget
```

## TerraMainPanel Layout

```
+--[Title Bar: "Terra Namp" + PlayMode indicator]--+  <- DragBox (30px)
|  Song Title (clipped) / Author (pink)            |
|  [=========>-----------] SeekBar  0:42 / 3:15    |
|  ||||||||||||||||||||||||||| Visualizer (48 bars) |
|  [|<] [<<] [>||] [>>] [>|]  [~]    [|] Volume   |
|  [Tab: Player] [Tab: Add] [Tab: Settings]        |
|  +-- Content area (tab-dependent) --------+      |
|  |  Song list / Add tracks / Settings /   |      |
|  |  Admin panel (shield button)           |      |
|  +----------------------------------------+      |
+--------------------------------------------------+
```

### Title Bar Buttons

```
[Title: "Terra Namp" + PlayMode]  [Shield]  [Settings]
```

- **Shield button** (`IconButton`): Opens admin panel. Visible only in multiplayer when local player is Admin. Always Append-ed, hidden offscreen when not applicable (see ui-rendering.md).
- **Settings button** (`IconButton`): Opens settings tab.

| Component | Class | Purpose |
|-----------|-------|---------|
| Title Bar | Manual draw | Drag handle, "Terra Namp" + play mode |
| SeekBar | `SeekBar` | Draggable progress bar, time display |
| Visualizer | `Visualizer` | 48-bar mirrored waveform from audio buffer |
| Controls | `IconButton` / `PlayPauseButton` | Prev, -10s, Play/Pause, +10s, Next |
| Mode | `IconToggleButton` | Cycles: Once -> Autoplay -> Shuffle -> Loop |
| Volume | `VolumeSlider` | Horizontal slider, persisted, logarithmic |
| Search | `SearchField` | Text input in Draw() phase |
| Song List | `ScrollableSongList` | Click=play, right-click=delete, scrollbar |

## Drawing Patterns

**Scissor Clipping** (text overflow):
```csharp
spriteBatch.End();
spriteBatch.Begin(SpriteSortMode.Immediate, ..., rasterizerWithScissor, ..., Main.UIScaleMatrix);
GraphicsDevice.ScissorRectangle = new Rectangle(
    (int)(clipRect.X * xScale), (int)(clipRect.Y * yScale),
    (int)(clipRect.Width * xScale), (int)(clipRect.Height * yScale));
// draw clipped content
GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
spriteBatch.End();
spriteBatch.Begin(SpriteSortMode.Deferred, ..., Main.UIScaleMatrix);
```

**Press Animation** (PressAnimator):
```csharp
private readonly PressAnimator pressAnim = new();

public override void Draw(SpriteBatch spriteBatch)
{
    pressAnim.Update(IsMouseHovering && Main.mouseLeft);
    Rectangle bounds = pressAnim.GetAnimatedBounds(GetDimensions().ToRectangle());
    // draw using animated bounds
}
```

**Rounded Rectangles:** `DrawingUtils.DrawRoundedRect/DrawRoundedBorder` for pixel-based rounded corners.

**Theme Boxes:** `MusicalBoxProvider` uses 9-slice rendering (6px corners, 4px edges).

## NowPlaying Popup

Slides in from the left when a new song starts.

```
State: Opening -> Waiting -> Closing
       xOffset++  timer++   xOffset--
       (20px/frame)         (20px/frame)
```

Duration: `max(name.Length * 0.2 * 60, 5 * 60)` frames.
