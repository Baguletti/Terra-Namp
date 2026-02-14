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

public class ColorPicker : SmartUIElement
{
    private const int SliderHeight = 24;
    private const int SliderGap = 4;
    private const int PreviewSize = 48;
    private const int PreviewMargin = 10;

    public string Label { get; set; } = "";
    public Color PickedColor { get; private set; }

    public event Action<Color> OnColorChanged;

    private HorizontalSlider rSlider;
    private HorizontalSlider gSlider;
    private HorizontalSlider bSlider;

    public void SetColor(Color color)
    {
        PickedColor = color;
        if (rSlider != null)
        {
            rSlider.Value = color.R;
            gSlider.Value = color.G;
            bSlider.Value = color.B;
        }
    }

    public override void OnInitialize()
    {
        int y = 20;

        // Red slider
        rSlider = new HorizontalSlider
        {
            Label = "R",
            Value = PickedColor.R,
            MinValue = 0,
            MaxValue = 255,
            Steps = 255,
            FormatValue = v => $"{(int)v}"
        };
        rSlider.Left.Set(0, 0);
        rSlider.Top.Set(y, 0);
        rSlider.Width.Set(0, 1f);
        rSlider.Width.Set(-PreviewSize - PreviewMargin, 1f);
        rSlider.Height.Set(SliderHeight, 0);
        rSlider.OnValueChanged += v =>
        {
            PickedColor = new Color((int)v, PickedColor.G, PickedColor.B);
            OnColorChanged?.Invoke(PickedColor);
        };
        Append(rSlider);
        y += SliderHeight + SliderGap;

        // Green slider
        gSlider = new HorizontalSlider
        {
            Label = "G",
            Value = PickedColor.G,
            MinValue = 0,
            MaxValue = 255,
            Steps = 255,
            FormatValue = v => $"{(int)v}"
        };
        gSlider.Left.Set(0, 0);
        gSlider.Top.Set(y, 0);
        gSlider.Width.Set(0, 1f);
        gSlider.Width.Set(-PreviewSize - PreviewMargin, 1f);
        gSlider.Height.Set(SliderHeight, 0);
        gSlider.OnValueChanged += v =>
        {
            PickedColor = new Color(PickedColor.R, (int)v, PickedColor.B);
            OnColorChanged?.Invoke(PickedColor);
        };
        Append(gSlider);
        y += SliderHeight + SliderGap;

        // Blue slider
        bSlider = new HorizontalSlider
        {
            Label = "B",
            Value = PickedColor.B,
            MinValue = 0,
            MaxValue = 255,
            Steps = 255,
            FormatValue = v => $"{(int)v}"
        };
        bSlider.Left.Set(0, 0);
        bSlider.Top.Set(y, 0);
        bSlider.Width.Set(0, 1f);
        bSlider.Width.Set(-PreviewSize - PreviewMargin, 1f);
        bSlider.Height.Set(SliderHeight, 0);
        bSlider.OnValueChanged += v =>
        {
            PickedColor = new Color(PickedColor.R, PickedColor.G, (int)v);
            OnColorChanged?.Invoke(PickedColor);
        };
        Append(bSlider);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;

        // Label
        float labelScale = 0.6f;
        spriteBatch.DrawString(font, Label,
            new Vector2(bounds.X, bounds.Y),
            Color.White * 0.7f, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

        // Color preview box (right side)
        Rectangle previewRect = new(
            bounds.X + bounds.Width - PreviewSize,
            bounds.Y + 20,
            PreviewSize,
            PreviewSize);

        DrawingUtils.DrawRoundedRect(spriteBatch, previewRect, PickedColor, 4);
        DrawingUtils.DrawRoundedBorder(spriteBatch, previewRect, Color.White * 0.3f, 4);

        base.Draw(spriteBatch);
    }
}
