# Window Bounds & Anti-Clipping

## DraggableUIElement — система позиционирования

`DraggableUIElement` — базовый класс для перетаскиваемых панелей (TerraMainPanel, MiniPlayerPanel). Вся логика границ сосредоточена в нём.

### Уровни защиты от выхода за экран

```
SetPositionDirect() / DefaultPosition
        |
        v
    basePos (Vector2?)
        |
        v
    Draw() — CLAMP перед AdjustPositions    <-- главный рубеж
        |
        v
    AdjustPositions() → Left.Set / Top.Set
        |
        v
    Recalculate() → финальные dimensions
```

**Четыре уровня clamping:**

1. **Draw() — финальный clamp (каждый кадр)**
   ```csharp
   if (basePos.HasValue && basePos.Value.X > -1000)
   {
       basePos = new Vector2(
           MathHelper.Clamp(basePos.Value.X, 0, Main.screenWidth - size.Width),
           MathHelper.Clamp(basePos.Value.Y, 0, Main.screenHeight - size.Height));
   }
   ```
   Перехватывает ВСЕ случаи: перетаскивание, программное позиционирование, смену режимов.

2. **Draw() при перетаскивании** — clamp курсорной позиции.

3. **SafeUpdate — инициализация** (`basePos == null`) — `ClampedDefaultBasePos()`.

4. **SafeUpdate — offscreen reset** — `ClampedDefaultBasePos()` когда панель случайно вылетела за экран.

### Формула clamp

```
x ∈ [0, screenWidth - panelWidth]
y ∈ [0, screenHeight - panelHeight]
```

Панель гарантированно полностью внутри экрана. Ни один пиксель не выходит за границы.

### Скрытые панели (offscreen hiding)

Панели скрываются через `SetPositionDirect(-9999, 0)`. Clamp пропускает панели с `basePos.X < -1000`:

```csharp
// Clamp НЕ применяется:
panel.SetPositionDirect(-9999, 0);  // intentionally hidden

// Clamp применяется:
panel.SetPositionDirect(100, 200);  // visible position
```

Offscreen reset в SafeUpdate тоже пропускает hidden панели:
```csharp
bool intentionallyHidden = basePos.HasValue && basePos.Value.X < -1000;
if (!dragging && !intentionallyHidden && !size.Intersects(screenRect))
    // reset to default
```

## Переключение режимов (TerraState)

### Три режима: Full → Mini → Hidden

```
Hidden ──hotkey──> Full ──hotkey──> Mini ──hotkey──> Hidden
                                     │
                              (MiniPlayerEnabled=false)
                                     │
                   Full ──hotkey──> Hidden
```

### Выравнивание позиций между режимами

**Проблема:** панели разного размера (Full: 340x520, Mini: 340x86). Если мини-плеер у нижней границы экрана (y=994 при 1080p), полный плеер с тем же top-left уходит за экран (y=994, bottom=1514).

**Решение:** `lastVisibleTopLeft` хранит top-left последней видимой панели. При переключении позиция **кламповится под размер новой панели**:

```csharp
// SwitchToMini: сохраняет позицию полного плеера
lastVisibleTopLeft = new Vector2(fullDims.X, fullDims.Y);

// ShowFullPanel: использует сохранённую позицию + clamp
x = MathHelper.Clamp(x, 0, Main.screenWidth - TerraMainPanel.PanelWidth);
y = MathHelper.Clamp(y, 0, Main.screenHeight - TerraMainPanel.PanelHeight);

// HideAll: сохраняет позицию видимой панели перед скрытием
if (miniDims.X > -1000)
    lastVisibleTopLeft = new Vector2(miniDims.X, miniDims.Y);
else if (fullDims.X > -1000)
    lastVisibleTopLeft = new Vector2(fullDims.X, fullDims.Y);
```

### Правило: НЕ использовать center-based позиционирование между панелями разного размера

DefaultPosition хранит центр панели (для совместимости с сохранением). Но при переключении между Full и Mini **всегда использовать top-left**. Center-based приводит к смещению:

```
Mini center (y=43 at top) → Full top-left: y = 43 - 260 = -217  // ЗА ЭКРАНОМ
Mini top-left (y=0)       → Full top-left: y = 0, clamped       // Корректно
```

## Чеклист для новых DraggableUIElement

1. Задать `Width.Set()` и `Height.Set()` — без них clamp не знает размер панели
2. Реализовать `DragBox` — область перетаскивания (обычно title bar)
3. Реализовать `DefaultPosition` — начальная позиция (normalized 0-1, center-based)
4. `OnDragEnd` — сохранять позицию в store если нужна персистентность
5. Если панель участвует в mode switching — добавить clamp в TerraState при переключении
6. Для offscreen hiding использовать `SetPositionDirect(-9999, 0)` (НЕ `Left.Set(-9999, 0)`)
7. Не вызывать `Left.Set` / `Top.Set` напрямую — вся позиция через `basePos` → `AdjustPositions`
