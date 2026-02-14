using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class VolumeSlider : SmartUIElement
{
    private const int TrackHeight = 4;
    private const int ThumbWidth = 10;
    private const int ThumbHeight = 16;
    private const int CornerRadius = 3;
    private const float FineSensitivity = 0.1f;

    private bool dragging;
    private float fineStartVolume;
    private float fineStartMouseX;
    private bool wasFineControl;

    public float Volume
    {
        get => PersistentDataStoreSystem.GetDataStore<TerraDataStore>().VolumeLevel;
        set => PersistentDataStoreSystem.GetDataStore<TerraDataStore>().VolumeLevel = MathHelper.Clamp(value, 0f, 1f);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        Color sliderColor = store.PanelColor;

        // Volume percentage label
        float labelScale = 0.55f;
        string label = $"{(int)(Volume * 100)}%";
        Vector2 labelSize = font.MeasureString(label) * labelScale;
        float labelX = bounds.X + bounds.Width - labelSize.X;
        float labelY = bounds.Y + (bounds.Height - labelSize.Y) / 2f;

        Color labelColor = (IsMouseHovering || dragging) ? sliderColor : Color.White * 0.5f;
        spriteBatch.DrawString(font, label, new Vector2(labelX, labelY), labelColor, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

        // Track area (to the left of the label)
        int trackAreaWidth = bounds.Width - (int)labelSize.X - 6;
        int trackY = bounds.Y + (bounds.Height - TrackHeight) / 2;

        // Track background
        Rectangle trackRect = new(bounds.X, trackY, trackAreaWidth, TrackHeight);
        DrawingUtils.DrawRoundedRect(spriteBatch, trackRect, Color.White * 0.15f, 2);

        // Track fill
        int fillWidth = (int)(trackAreaWidth * Volume);
        if (fillWidth > 0)
        {
            Rectangle fillRect = new(bounds.X, trackY, fillWidth, TrackHeight);
            DrawingUtils.DrawRoundedRect(spriteBatch, fillRect, sliderColor * 0.5f, 2);
        }

        // Thumb
        int thumbX = bounds.X + fillWidth - ThumbWidth / 2;
        thumbX = (int)MathHelper.Clamp(thumbX, bounds.X, bounds.X + trackAreaWidth - ThumbWidth);
        int thumbY = bounds.Y + (bounds.Height - ThumbHeight) / 2;
        Rectangle thumbRect = new(thumbX, thumbY, ThumbWidth, ThumbHeight);

        Color thumbColor = (IsMouseHovering || dragging) ? sliderColor : sliderColor * 0.7f;
        DrawingUtils.DrawRoundedRect(spriteBatch, thumbRect, thumbColor, CornerRadius);

        base.Draw(spriteBatch);
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        if (!Main.mouseLeft && dragging)
        {
            dragging = false;
            wasFineControl = false;
            PersistentDataStoreSystem.GetDataStore<TerraDataStore>().ForceSave();
        }

        if (dragging)
            UpdateVolumeFromMouse();
    }

    public override void SafeMouseDown(UIMouseEvent evt)
    {
        dragging = true;
        wasFineControl = false;
        UpdateVolumeFromMouse();
    }

    private void UpdateVolumeFromMouse()
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        float labelScale = 0.55f;
        float labelWidth = font.MeasureString("100%").X * labelScale;
        int trackAreaWidth = bounds.Width - (int)labelWidth - 6;

        bool fineControl = Main.keyState.IsKeyDown(Keys.LeftShift) ||
                           Main.keyState.IsKeyDown(Keys.RightShift);

        if (fineControl)
        {
            if (!wasFineControl)
            {
                fineStartVolume = Volume;
                fineStartMouseX = Main.MouseScreen.X;
                wasFineControl = true;
            }

            float mouseDelta = Main.MouseScreen.X - fineStartMouseX;
            float normalizedDelta = mouseDelta / trackAreaWidth;
            Volume = MathHelper.Clamp(fineStartVolume + normalizedDelta * FineSensitivity, 0f, 1f);
        }
        else
        {
            wasFineControl = false;
            float relativeX = Main.MouseScreen.X - bounds.X;
            Volume = MathHelper.Clamp(relativeX / trackAreaWidth, 0f, 1f);
        }
    }
}
