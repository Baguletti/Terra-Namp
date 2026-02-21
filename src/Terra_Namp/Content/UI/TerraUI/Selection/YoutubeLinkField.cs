// Credit to Scalie for TextField - https://github.com/ScalarVector1/DragonLens/blob/master/Content/GUI/FieldEditors/TextField.cs

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Components;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.UI;
using ReLogic.Localization.IME;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using Terraria;
using ReLogic.OS;
using System;
using Terra_Namp.Localization;
using Microsoft.Xna.Framework.Input;

namespace Terra_Namp.Content.UI.TerraUI.Selection;

public class YoutubeLinkField : SmartUIElement
{
    private const int CornerRadius = 4;

    public Color? HighlightColor { get; set; } // null = default style, not null = highlight during download

    public string CurrentValue
    {
        get => input.Text;
        set => input.Text = value;
    }

    private readonly TextInputHandler input = new();
    private bool typing;
    private bool updated;
    private bool reset;

    public void SetTyping()
    {
        typing = true;
        Main.blockInput = true;
    }

    public void SetNotTyping()
    {
        typing = false;
        Main.blockInput = false;
    }

    public override void SafeClick(UIMouseEvent evt)
    {
        SetTyping();
        var bounds = GetDimensions().ToRectangle();
        input.SetCursorFromClickPlain(FontAssets.MouseText.Value, 0.85f, bounds.X + 8, Main.MouseScreen.X);
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        if (reset)
        {
            updated = false;
            reset = false;
        }

        if (updated)
        {
            reset = true;
        }

        if (Main.mouseLeft && !IsMouseHovering)
        {
            SetNotTyping();
        }
    }

    private void HandleText()
    {
        if (Main.keyState.IsKeyDown(Keys.Escape))
        {
            SetNotTyping();
        }

        string old = input.Text;
        input.HandleInput();

        if (input.Text != old)
            updated = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();

        // Background: highlight if downloading, otherwise default style
        Color bgColor = HighlightColor.HasValue
            ? HighlightColor.Value
            : Color.Black * 0.3f;
        DrawingUtils.DrawRoundedRect(spriteBatch, bounds, bgColor, CornerRadius);

        // Border: accent if typing, subtle if not, highlight color during download
        Color borderColor;
        if (typing)
            borderColor = store.PanelColor * 0.5f;
        else if (HighlightColor.HasValue)
            borderColor = HighlightColor.Value;
        else
            borderColor = Color.White * 0.1f;

        DrawingUtils.DrawRoundedBorder(spriteBatch, bounds, borderColor, CornerRadius);

        if (typing)
        {
            HandleText();

            // draw ime panel, note that if there's no composition string then it won't draw anything
            Main.instance.DrawWindowsIMEPanel(GetDimensions().Position());
        }

        RasterizerState state = new()
        {
            ScissorTestEnable = true,
            CullMode = CullMode.None
        };

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, state,
            null, Main.UIScaleMatrix);

        float xScale = Main.UIScaleMatrix.M11;
        float yScale = Main.UIScaleMatrix.M22;

        Rectangle drawBox = GetDimensions().ToRectangle();

        Rectangle rectangle = new(drawBox.X + 8, drawBox.Y, drawBox.Width - 16, drawBox.Height);

        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(
            (int)(rectangle.X * xScale),
            (int)(rectangle.Y * yScale),
            (int)(rectangle.Width * xScale),
            (int)(rectangle.Height * yScale)
        );

        const float scale = 0.85f;

        string displayed = input.Text ?? "";

        int stringWidth = (int)(FontAssets.MouseText.Value.MeasureString(displayed).X * scale);

        // Scroll text so cursor is always visible
        string beforeCursor = displayed[..Math.Min(input.CursorPos, displayed.Length)];
        int cursorPixelX = (int)(FontAssets.MouseText.Value.MeasureString(beforeCursor).X * scale);
        int fieldWidth = drawBox.Width - 16;
        float positionOffset = Math.Max(cursorPixelX - fieldWidth + 8, 0);

        Vector2 pos = GetDimensions().Position() + Vector2.One * 8 - new Vector2(positionOffset, 0);

        Utils.DrawBorderString(spriteBatch, displayed, pos, Color.White, scale);

        if (!typing)
        {
            RestartSpriteBatch(spriteBatch);
            return;
        }

        // IME composition string
        float cursorDrawX = pos.X + cursorPixelX;
        string compositionString = Platform.Get<IImeService>().CompositionString;

        if (compositionString is { Length: > 0 })
        {
            Utils.DrawBorderString(spriteBatch, compositionString, new Vector2(cursorDrawX, pos.Y), new Color(255, 240, 20), scale);
            cursorDrawX += FontAssets.MouseText.Value.MeasureString(compositionString).X * scale;
        }

        if (Main.GameUpdateCount % 20 < 10)
            Utils.DrawBorderString(spriteBatch, "|", new Vector2(cursorDrawX, pos.Y), Color.White, scale);

        RestartSpriteBatch(spriteBatch);

        if (IsMouseHovering)
        {
            Main.instance.MouseText(LocalizationHelper.GetGUIText("TerraMenu.FileDialogTooltip"));
        }
    }

    private void RestartSpriteBatch(SpriteBatch spriteBatch)
    {
        Main.instance.GraphicsDevice.ScissorRectangle = Main.instance.GraphicsDevice.Viewport.Bounds;

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
            null, Main.UIScaleMatrix);
    }
}
