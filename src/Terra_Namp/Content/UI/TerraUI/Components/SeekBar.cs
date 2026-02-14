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

public class SeekBar : SmartUIElement
{
    private bool dragging;

    public float Progress { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan Duration { get; set; }

    public event Action<float> OnSeek;

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        Color accentColor = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;

        // Track background
        int barHeight = 4;
        int barY = bounds.Y;
        DrawingUtils.DrawRoundedRect(spriteBatch,
            new Rectangle(bounds.X, barY, bounds.Width, barHeight),
            Color.White * 0.15f, 2);

        // Fill (progress bar)
        int fillWidth = (int)(bounds.Width * Progress);
        if (fillWidth > 0)
        {
            DrawingUtils.DrawRoundedRect(spriteBatch,
                new Rectangle(bounds.X, barY, fillWidth, barHeight),
                accentColor * 0.8f, 2);
        }

        // Caret (thumb)
        int caretX = bounds.X + fillWidth;
        DrawingUtils.DrawRoundedRect(spriteBatch,
            new Rectangle(caretX - 1, barY - 3, 3, barHeight + 6),
            accentColor, 1);

        // Time text
        string timeText = $"{FormatTime(ElapsedTime)} / {FormatTime(Duration)}";
        float scale = 0.6f;
        Vector2 textSize = font.MeasureString(timeText) * scale;
        spriteBatch.DrawString(font, timeText,
            new Vector2(bounds.X + (bounds.Width - textSize.X) / 2f, barY + barHeight + 4),
            accentColor * 0.7f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        base.Draw(spriteBatch);
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        if (!Main.mouseLeft && dragging)
            dragging = false;

        if (dragging)
        {
            Rectangle bounds = GetDimensions().ToRectangle();
            float relativeX = Main.MouseScreen.X - bounds.X;
            OnSeek?.Invoke(MathHelper.Clamp(relativeX / bounds.Width, 0f, 1f));
        }
    }

    public override void SafeMouseDown(UIMouseEvent evt)
    {
        dragging = true;
        Rectangle bounds = GetDimensions().ToRectangle();
        float relativeX = Main.MouseScreen.X - bounds.X;
        OnSeek?.Invoke(MathHelper.Clamp(relativeX / bounds.Width, 0f, 1f));
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.Hours > 0)
            return $"{time.Hours}:{time.Minutes:D2}:{time.Seconds:D2}";
        return $"{time.Minutes}:{time.Seconds:D2}";
    }
}
