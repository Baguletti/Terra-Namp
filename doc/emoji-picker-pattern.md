# Emoji Picker — Scrollable Grid Pattern

## Problem
Rendering a scrollable grid of sprites from a texture atlas inside a fixed-size popup. Common pitfalls:
- Rows "leaking" past popup boundaries (scissor clipping unreliable in tModLoader)
- Pixel artifacts (halo/border) around atlas sprites when scaling
- Fractional scroll causing partial rows

## Correct Approach: Row-Indexed Rendering

**Never** offset all sprites by a scroll float and hope scissor clips them. Instead, compute which rows are visible and draw **exactly** those rows at fixed positions inside the clip area.

### Core Logic

```csharp
int firstRow = (int)scrollOffset / CellSize;   // which data row is first visible

for (int visRow = 0; visRow < VisibleRows; visRow++)
{
    int dataRow = firstRow + visRow;
    if (dataRow >= totalRows) break;

    for (int col = 0; col < Cols; col++)
    {
        int idx = dataRow * Cols + col;
        if (idx >= items.Length) break;

        // Position is relative to the clip area, NOT to virtual content
        int x = clipRect.X + col * CellSize;
        int y = clipRect.Y + visRow * CellSize;

        // Draw item at (x, y)...
    }
}
```

### Key Principles

1. **Discrete scroll**: `scrollOffset` is always a multiple of `CellSize`. Each scroll wheel step = exactly 1 row.
2. **Fixed visible rows**: `VisibleRows` is a constant (e.g., 6). Exactly that many rows are drawn.
3. **Position = clip area relative**: `clipRect.Y + visRow * CellSize` — never goes outside bounds by construction.
4. **No scissor dependency**: Works correctly even if GPU scissor test is disabled.
5. **Hit testing matches drawing**: `HandleClick` uses the same `firstRow` + `visRow` loop.

### Scroll Clamping

```csharp
int maxScrollRows = Math.Max(0, totalRows - VisibleRows);
scrollOffset = MathHelper.Clamp(scrollOffset, 0, maxScrollRows * CellSize);
```

### Sizing

```
PopupHeight = VisibleRows * CellSize + padding (top + bottom)
clipRect.Height = VisibleRows * CellSize  (exact, no fractional rows)
```

## Dynamic Column Count

Picker width is dynamic (fills the parent panel). Columns are computed at draw time:

```csharp
int pickerWidth = panelBounds.Width - 2 * Padding;
int cols = (pickerWidth - 8 - ScrollbarHitWidth) / CellSize;
```

This means `cols` is a field (not a constant) and must be used consistently in Draw, Click, and ScrollWheel handlers. Guard against `cols <= 0` before any division.

## Draggable Scrollbar

Pattern copied from `ScrollablePanel`. Three methods cooperate:

### SafeMouseDown — start drag

```csharp
// Hit area is wider than the visual bar for easier clicking
Rectangle scrollHitRect = new(clipRight - HitWidth, clipY, HitWidth, visibleHeight);
if (!scrollHitRect.Contains(mouse)) return;

// Calculate thumb position
float barHeight = Max(20, visibleHeight * visibleHeight / totalContentHeight);
float barY = clipY + (scrollOffset / maxScroll) * (visibleHeight - barHeight);
Rectangle thumbRect = new(scrollHitRect.X, (int)barY, scrollHitRect.Width, (int)barHeight);

scrollDragging = true;
if (thumbRect.Contains(mouse))
    dragOffsetY = mouse.Y - barY;          // grab thumb directly
else
{
    dragOffsetY = barHeight / 2f;           // center thumb on cursor
    // immediately jump to click position
}
```

### SafeUpdate — continue drag

```csharp
if (scrollDragging)
{
    if (!Main.mouseLeft) { scrollDragging = false; return; }
    float trackHeight = visibleHeight - barHeight;
    float mouseRelY = Main.MouseScreen.Y - dragOffsetY - clipY;
    float ratio = MathHelper.Clamp(mouseRelY / trackHeight, 0f, 1f);
    scrollOffset = ratio * maxScroll;
}
```

### Draw — visual feedback

```csharp
bool scrollHover = scrollHitRect.Contains(mouse) || scrollDragging;
int barW = scrollHover ? 5 : 3;    // expand on hover
Color barColor = scrollHover ? accent * 0.6f : accent * 0.4f;
DrawingUtils.DrawRoundedRect(sb, scrollBar, barColor, barW / 2);
```

Block emoji hover during drag: `bool hover = cellRect.Contains(mouse) && !scrollDragging;`

## Atlas Sprite Artifacts

When drawing sprites from a texture atlas at a different size than their source, semi-transparent edge pixels become visible as a halo/border.

### Fixes

1. **Inset source rect** by 2px on each side to cut off anti-aliased edge pixels:
   ```csharp
   Rectangle insetSrc = new(src.X + 2, src.Y + 2, src.Width - 4, src.Height - 4);
   ```

2. **Scale to ~75%** of cell size — reduces visible artifacts and adds visual padding:
   ```csharp
   int spriteSize = CellSize * 3 / 4;  // 24 for 32px cells
   int pad = (CellSize - spriteSize) / 2;
   Rectangle dest = new(x + pad, y + pad, spriteSize, spriteSize);
   ```

3. **Use `SamplerState.LinearClamp`** for smooth downscaling (not PointClamp which creates jagged edges).

## Rounded Corners

Use `DrawRoundedRect` (SDF shader) for the background, then draw content inside a 4px inset area. The 4px padding is larger than the corner radius overlap, so no content appears in the rounded corner region.

```
1. DrawRoundedRect(bounds, Color.Black, 6)     // opaque rounded background
2. [scissor section] draw grid content          // inside 4px-inset clip rect
3. DrawRoundedBorder(bounds, accent * 0.3f, 6)  // border on top after restore
```

## Emoji Category Sorting

Emojis sorted by `GetEmojiCategory()` (messenger-style order), then by codepoint within each category:

| Priority | Category | Unicode ranges |
|----------|----------|---------------|
| 0 | Smileys & Emotion | 1F600-1F64F, 1F910-1F92F, 1F970-1F97A |
| 1 | Hearts & Gestures | 1F44D-1F450, 1F490-1F49F, 270A-270D |
| 2 | People | 1F466-1F487, 1F930-1F93A, 1F9B0-1F9DD |
| 3 | Animals | 1F400-1F43F, 1F980-1F9AE |
| 4 | Nature & Weather | 1F300-1F321, 1F330-1F344, 2600-2614 |
| 5 | Food & Drink | 1F345-1F37F, 1F950-1F96F |
| 6 | Activities | 1F3A0-1F3CE, 1F93C-1F945 |
| 7 | Travel & Places | 1F680-1F6FF, 1F3CF-1F3F0 |
| 8 | Objects | 1F380-1F39F, 1F4A0-1F53D |
| 9 | Symbols & other | everything else |

Implemented in `EmojiRenderer.GetEmojiCategory()`. Sorting happens once in `GetSortedCodepoints()` and is cached.
