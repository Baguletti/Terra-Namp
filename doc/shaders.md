# Shaders in tModLoader

## Вывод для Terra Namp

Для маленьких UI элементов (визуализатор, кнопки) pixel-based рендеринг лучше шейдеров: нет проблем с совместимостью, не нужна компиляция .xnb, полный контроль.

## Когда использовать шейдеры

- **Armor Shaders** — краски для предметов (`GameShaders.Armor.BindShader()`)
- **Screen Shaders** — полноэкранные эффекты: боссы, биомы (`Filters.Scene[]`)
- **Misc Shaders** — эффекты для проектайлов/NPC (`GameShaders.Misc[]`)

## Когда НЕ использовать

- UI элементы < 1000 пикселей
- Простые геометрические формы
- Когда pixel-based рисование проще

## Misc Shader пример (если понадобится)

```csharp
// Регистрация в Load():
GameShaders.Misc["Terra_Namp:Effect"] = new MiscShaderData(
    new Ref<Effect>(Assets.Request<Effect>("Assets/Effects/MyEffect", AssetRequestMode.ImmediateLoad).Value),
    "TechniqueName");

// Применение:
spriteBatch.End();
spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
    SamplerState.LinearClamp, DepthStencilState.None,
    RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);
GameShaders.Misc["Terra_Namp:Effect"].Apply();
// рисуем...
spriteBatch.End();
// восстанавливаем стандартный batch
```

## Компиляция .fx -> .xnb

```bash
# Content.mgcb:
/platform:DesktopGL
/profile:Reach
/build:MyShader.fx

# Запуск:
mgcb Content.mgcb
# Копировать .xnb в Assets/Effects/
```

Альтернативы: EasyXnb (GUI), DyeLab (с preview).
