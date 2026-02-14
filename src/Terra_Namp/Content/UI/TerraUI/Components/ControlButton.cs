using Terra_Namp.Core.IO;
using Terra_Namp.Content.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class ControlButton : SmartUIElement
{
    private const int CornerRadius = 4;
    private const float ScrollSpeed = 0.5f; // pixels per frame
    private const int ScrollPadding = 20; // space between end and start of loop
    private static float referenceHeight = -1;

    private readonly PressAnimator pressAnim = new();
    private string label;
    private float scrollOffset;
    private bool needsScrolling;

    public ControlButton(string label)
    {
        this.label = label;
    }

    public void SetText(string text)
    {
        label = text;
        scrollOffset = 0; // Reset scroll when text changes
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        pressAnim.Update(IsMouseHovering && Main.mouseLeft);
        Rectangle bounds = pressAnim.GetAnimatedBounds(GetDimensions().ToRectangle());
        var font = FontAssets.MouseText.Value;
        float scale = 0.65f;

        Color bgColor = IsMouseHovering ? Color.White * 0.12f : Color.White * 0.05f;
        DrawingUtils.DrawRoundedRect(spriteBatch, bounds, bgColor, CornerRadius);

        if (IsMouseHovering)
            DrawingUtils.DrawRoundedBorder(spriteBatch, bounds, PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor * 0.25f, CornerRadius);

        if (referenceHeight < 0)
            referenceHeight = font.MeasureString("A|><").Y * scale;

        Vector2 textSize = font.MeasureString(label) * scale;
        int availableWidth = bounds.Width - 12; // padding
        needsScrolling = textSize.X > availableWidth;

        Color textColor = IsMouseHovering ? PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor : Color.White * 0.8f;

        if (needsScrolling)
        {
            // Scrolling text
            scrollOffset += ScrollSpeed;
            float totalScrollWidth = textSize.X + ScrollPadding;
            if (scrollOffset > totalScrollWidth)
                scrollOffset = 0;

            // Enable scissor clipping
            RasterizerState scissorState = new() { ScissorTestEnable = true, CullMode = CullMode.None };
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, Main.UIScaleMatrix);

            float xScale = Main.UIScaleMatrix.M11;
            float yScale = Main.UIScaleMatrix.M22;

            Rectangle clipRect = new(bounds.X + 6, bounds.Y, availableWidth, bounds.Height);
            Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(
                (int)(clipRect.X * xScale), (int)(clipRect.Y * yScale),
                (int)(clipRect.Width * xScale), (int)(clipRect.Height * yScale));

            Vector2 pos1 = new(bounds.X + 6 - scrollOffset, bounds.Y + (bounds.Height - referenceHeight) / 2f);
            Vector2 pos2 = new(bounds.X + 6 - scrollOffset + totalScrollWidth, bounds.Y + (bounds.Height - referenceHeight) / 2f);

            spriteBatch.DrawString(font, label, pos1, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, label, pos2, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Restore default state
            Main.instance.GraphicsDevice.ScissorRectangle = Main.instance.GraphicsDevice.Viewport.Bounds;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
        else
        {
            // Centered text (original behavior)
            Vector2 pos = new(
                bounds.X + (bounds.Width - textSize.X) / 2f,
                bounds.Y + (bounds.Height - referenceHeight) / 2f);
            spriteBatch.DrawString(font, label, pos, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        base.Draw(spriteBatch);
    }
}
