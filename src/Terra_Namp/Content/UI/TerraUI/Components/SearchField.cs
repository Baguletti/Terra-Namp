using Terra_Namp.Core.IO;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terra_Namp.Common.UI.Abstract;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class SearchField : SmartUIElement
{
    private const int CornerRadius = 4;

    public string Text
    {
        get => input.Text;
        set => input.Text = value;
    }

    public event Action<string> OnTextChanged;

    private readonly TextInputHandler input = new();
    private bool focused;

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        float scale = 0.7f;

        // Background
        DrawingUtils.DrawRoundedRect(spriteBatch, bounds, Color.Black * 0.3f, CornerRadius);

        // Border
        Color borderColor = focused ? PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor * 0.5f : Color.White * 0.1f;
        DrawingUtils.DrawRoundedBorder(spriteBatch, bounds, borderColor, CornerRadius);

        // Handle text input during Draw
        if (focused)
        {
            string old = input.Text;
            input.HandleInput();

            if (input.Text != old)
                OnTextChanged?.Invoke(input.Text);

            if (Main.keyState.IsKeyDown(Keys.Escape))
            {
                Unfocus();
            }
        }

        // Text
        bool isPlaceholder = string.IsNullOrEmpty(input.Text);
        string displayText = isPlaceholder ? "Search..." : input.Text;
        Color textColor = isPlaceholder ? Color.White * 0.3f : Color.White * 0.85f;

        float textY = bounds.Y + (bounds.Height - font.MeasureString("A").Y * scale) / 2f;
        Vector2 textPos;

        if (isPlaceholder && !focused)
        {
            Vector2 placeholderSize = font.MeasureString(displayText) * scale;
            textPos = new Vector2(bounds.X + (bounds.Width - placeholderSize.X) / 2f, textY);
        }
        else
        {
            textPos = new Vector2(bounds.X + 8, textY);
        }

        spriteBatch.DrawString(font, displayText, textPos, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        // Cursor
        if (focused)
            input.DrawCursorPlain(spriteBatch, font, scale, bounds.X + 8, bounds.Y, bounds.Height,
                PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor);

        base.Draw(spriteBatch);
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        if (focused)
            Main.blockInput = true;

        if (Main.mouseLeft && !IsMouseHovering && focused)
            Unfocus();
    }

    public override void SafeClick(UIMouseEvent evt)
    {
        focused = true;
        var bounds = GetDimensions().ToRectangle();
        input.SetCursorFromClickPlain(FontAssets.MouseText.Value, 0.7f, bounds.X + 8, Main.MouseScreen.X);
    }

    public void Unfocus()
    {
        focused = false;
        Main.blockInput = false;
    }
}
