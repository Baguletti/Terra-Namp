# UI Rendering Guide

## Главное правило

**В `Draw()` SpriteBatch УЖЕ активен.** Не вызывай `Begin()`/`End()` без причины.

```csharp
// 90% случаев — просто рисуй:
spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, color);
spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
```

## Scissor Clipping (обрезка контента)

Единственный частый случай когда нужно менять batch:

```csharp
Rectangle prevScissor = Main.instance.GraphicsDevice.ScissorRectangle;
spriteBatch.End();

RasterizerState rState = new() { ScissorTestEnable = true, CullMode = CullMode.None };
spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
    DepthStencilState.None, rState, null, Main.UIScaleMatrix);

float xScale = Main.UIScaleMatrix.M11;
float yScale = Main.UIScaleMatrix.M22;
Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(
    (int)(clipRect.X * xScale), (int)(clipRect.Y * yScale),
    (int)(clipRect.Width * xScale), (int)(clipRect.Height * yScale));

// рисуем обрезанный контент...

Main.instance.GraphicsDevice.ScissorRectangle = prevScissor;
spriteBatch.End();
spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
```

## Стандартные параметры восстановления batch

```csharp
spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
```

## Запреты

- **RenderTarget2D в UI** — вызывает черный экран если не восстановить состояние. Используй pixel-based рендеринг.
- **`Matrix.Identity`** вместо `Main.UIScaleMatrix` — ломает UI координаты.
- **Незакрытый/непарный Begin/End** — ломает рендеринг остального UI.
- **Забытый scissor restore** — обрезает всё что рисуется после.

## Привязка контента к вкладкам

**Контент вкладки ВСЕГДА рисуется внутри дочернего UIElement этой вкладки, НИКОГДА в родительском DraggableDraw().**

### Проблема

Если рисовать контент вкладки (например, название трека) в `DraggableDraw()` родительской панели, он будет виден когда активна любая другая вкладка. Переключение вкладок убирает (`RemoveChild`) панель вкладки, но `DraggableDraw()` родителя вызывается всегда.

### Правильный паттерн

Контент вкладки = дочерний `SmartUIElement` внутри панели вкладки:

```csharp
// В SafeOnInitialize() родителя:
playerTab = new ScrollableSongList();
nowPlayingWidget = new NowPlayingWidget();
nowPlayingWidget.Left.Set(x, 0);
nowPlayingWidget.Top.Set(y, 0);
nowPlayingWidget.Width.Set(w, 0);
nowPlayingWidget.Height.Set(h, 0);
playerTab.Append(nowPlayingWidget);  // привязан к playerTab
```

Когда `playerTab` удаляется через `RemoveChild()`, все его дочерние элементы (включая `nowPlayingWidget`) перестают рисоваться автоматически.

### Если нужны данные из родителя

Передавай данные через свойства, а рисование оставь дочернему элементу:

```csharp
// В DraggableDraw() родителя — ТОЛЬКО обновление данных:
nowPlayingWidget.SongTitle = ActiveSong?.Name ?? "---";
nowPlayingWidget.SongAuthor = ActiveSong?.Author ?? "";

// В Draw() дочернего NowPlayingWidget — рисование:
public override void Draw(SpriteBatch spriteBatch)
{
    // TextBanner, scissor clipping и т.д.
    base.Draw(spriteBatch);
}
```

### Проверочный вопрос

> "Если я переключу вкладку, этот контент исчезнет?"

Если `Draw()` находится в дочернем элементе вкладки — да. Если в `DraggableDraw()` родителя — нет, баг.

## Скрытие элементов

**Offscreen-паттерн** — элемент всегда в дереве (текстуры загружены), но визуально скрыт:

```csharp
// В SafeOnInitialize():
shieldButton = new IconButton("Terra_Namp/Assets/UI/Icons/Shield", iconPadding: 4);
Append(shieldButton);  // ВСЕГДА в дереве — OnInitialize() загрузит текстуры

// В Draw()/Update():
if (shouldShow)
    shieldButton.Left.Set(normalX, 0);
else
    shieldButton.Left.Set(-9999, 0);  // за экраном
```

**Почему не Append/RemoveChild:** `OnInitialize()` вызывается только при добавлении в дерево. Если элемент не был в дереве — текстуры не загружены, иконка пустая.

## Fake Gaussian Blur для текста/эмодзи

`BlurHelper` блюрит только игровой мир (пре-рендеренный буфер), а НЕ UI-элементы из того же SpriteBatch-прохода. `RenderTarget2D` в UI опасен (черный экран). Для размытия UI-текста за overlay используется multi-pass offset rendering.

### Суть приёма

Текст отрисовывается ~40 раз со смещениями по X/Y, каждая копия с alpha по Гауссу от расстояния. Центральная (чёткая) копия **не рисуется**.

```csharp
bool behindOverlay = showOverlay && row < 3;

if (behindOverlay)
{
    for (int dx = -4; dx <= 4; dx++)
    {
        for (int dy = -4; dy <= 4; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            float dist2 = dx * dx + dy * dy;
            float weight = 0.055f * MathF.Exp(-dist2 / 8f);
            if (weight < 0.004f) continue;  // skip negligible corners
            EmojiRenderer.DrawString(sb, font, text,
                pos + new Vector2(dx, dy), color * weight, scale);
        }
    }
}
else
{
    EmojiRenderer.DrawString(sb, font, text, pos, color, scale);
}
```

### Параметры

| Параметр | Значение | Эффект |
|----------|----------|--------|
| Радиус | 4 (ядро 9x9) | Степень размытия |
| Базовый вес | 0.055 | Яркость размытого текста |
| Sigma (div в exp) | 8 | Ширина Гауссова колокола (больше = мягче) |
| Cutoff | 0.004 | Пропуск дальних углов (оптимизация) |

### Обязательное условие для эмодзи

`EmojiRenderer.DrawString()` рисует эмодзи через `sb.Draw(atlas, ..., Color.White)`. Для корректного размытия эмодзи-спрайты ДОЛЖНЫ получать alpha из `textColor`:

```csharp
// EmojiRenderer.cs — НЕ Color.White, а:
sb.Draw(atlas, dest, srcRect, Color.White * (textColor.A / 255f));
```

Без этого эмодзи остаются чёткими при любом количестве offset-копий.

### Производительность

Ядро 9x9 = 80 копий, cutoff отсекает ~15. Итого ~65 draw calls на один текстовый элемент. Для 15 падов = ~975 draw calls. Приемлемо, т.к. активно только когда overlay открыт.

### Альтернативы (и почему не подходят)

- **RenderTarget2D + blur shader** — правильный подход, но вызывает черный экран при неправильном восстановлении SpriteBatch состояния
- **Просто снижение alpha** — SDF-рендеринг (`DrawRoundedRect`) не перекрывает SpriteBatch-текст корректно
- **Не рисовать текст вообще** — некрасиво, пропадает визуальный контекст

## Позиционирование

**Всегда абсолютные координаты:**
```csharp
slider.Top.Set(y, 0);     // правильно: y пикселей от верха
slider.Top.Set(-50, 1f);  // НЕПРАВИЛЬНО: нестабильно в Terraria UI
```

**После создания дочерних элементов:**
```csharp
element.Activate();
element.Recalculate();
```

Без `Activate()` элементы могут не отвечать на клики или не отрисовываться.
