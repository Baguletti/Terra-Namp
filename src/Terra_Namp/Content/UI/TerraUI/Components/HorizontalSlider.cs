using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class HorizontalSlider : SmartUIElement
{
    private bool dragging;

    public string Label { get; set; } = "";
    public float Value { get; set; } = 0.5f;
    public float MinValue { get; set; }
    public float MaxValue { get; set; } = 1f;
    public int Steps { get; set; }
    public Func<float, string> FormatValue { get; set; }
    public bool IsDragging => dragging;

    public event Action<float> OnValueChanged;

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        Color accent = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;

        float labelScale = 0.6f;
        float valueScale = 0.55f;

        // Label on left
        spriteBatch.DrawString(font, Label,
            new Vector2(bounds.X, bounds.Y),
            Color.White * 0.7f, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

        // Value display on right
        string valueText = FormatValue != null ? FormatValue(Value) : $"{Value:F2}";
        Vector2 valueSize = font.MeasureString(valueText) * valueScale;
        spriteBatch.DrawString(font, valueText,
            new Vector2(bounds.X + bounds.Width - valueSize.X, bounds.Y),
            accent * 0.8f, 0f, Vector2.Zero, valueScale, SpriteEffects.None, 0f);

        // Slider track
        int trackY = bounds.Y + 18;
        int trackHeight = 4;
        DrawingUtils.DrawRoundedRect(spriteBatch,
            new Rectangle(bounds.X, trackY, bounds.Width, trackHeight),
            Color.White * 0.15f, 2);

        // Slider fill
        float normalized = (Value - MinValue) / (MaxValue - MinValue);
        int fillWidth = (int)(bounds.Width * normalized);
        if (fillWidth > 0)
        {
            DrawingUtils.DrawRoundedRect(spriteBatch,
                new Rectangle(bounds.X, trackY, fillWidth, trackHeight),
                accent * 0.5f, 2);
        }

        // Thumb
        int thumbX = bounds.X + fillWidth;
        int thumbWidth = 8;
        int thumbHeight = 12;
        DrawingUtils.DrawRoundedRect(spriteBatch,
            new Rectangle(thumbX - thumbWidth / 2, trackY - (thumbHeight - trackHeight) / 2, thumbWidth, thumbHeight),
            IsMouseHovering || dragging ? accent : Color.White * 0.8f, 3);

        base.Draw(spriteBatch);
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        if (!Main.mouseLeft && dragging)
        {
            dragging = false;
        }

        if (dragging)
        {
            UpdateValueFromMouse();
        }
    }

    public override void SafeMouseDown(UIMouseEvent evt)
    {
        dragging = true;
        UpdateValueFromMouse();
    }

    private void UpdateValueFromMouse()
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        float relativeX = Main.MouseScreen.X - bounds.X;
        float normalized = MathHelper.Clamp(relativeX / bounds.Width, 0f, 1f);
        float newValue = MinValue + normalized * (MaxValue - MinValue);

        if (Steps > 0)
        {
            float step = (MaxValue - MinValue) / Steps;
            newValue = MinValue + MathF.Round((newValue - MinValue) / step) * step;
        }

        newValue = MathHelper.Clamp(newValue, MinValue, MaxValue);

        if (Math.Abs(newValue - Value) > 0.001f)
        {
            Value = newValue;
            OnValueChanged?.Invoke(Value);
        }
    }
}
