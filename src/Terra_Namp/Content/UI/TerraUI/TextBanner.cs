using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;

namespace Terra_Namp.Content.UI.TerraUI
{
    public class TextBanner
    {
        private const float ScrollSpeed = 0.5f;
        private const int ScrollPadding = 40; // Space between end and start of loop

        private readonly string text;

        private readonly DynamicSpriteFont font;
        private readonly float scale;

        private readonly bool scrollingRequired;
        private readonly float textWidth;

        private float scrollOffset;

        public Rectangle Rectangle { get; private set; }

        public TextBanner(string text, Rectangle rectangle, DynamicSpriteFont font, float scale = 1f)
        {
            this.text = text;
            Rectangle = rectangle;
            this.font = font;
            this.scale = scale;

            textWidth = font.MeasureString(text).X * scale;
            scrollingRequired = textWidth > rectangle.Width - 8;
        }

        public void UpdateRectangle(Rectangle newRect)
        {
            Rectangle = newRect;
        }

        public void UpdateScrolling()
        {
            if (!scrollingRequired) return;

            // Infinite scrolling: text moves left, loops back when fully scrolled
            scrollOffset += ScrollSpeed;
            float totalScrollWidth = textWidth + ScrollPadding;
            if (scrollOffset > totalScrollWidth)
                scrollOffset = 0;
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 position, Color color)
        {
            if (!scrollingRequired)
            {
                spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            else
            {
                // Save previous scissor rectangle to restore it after drawing
                var prevScissor = Main.instance.GraphicsDevice.ScissorRectangle;

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

                Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(
                    (int)(Rectangle.X * xScale),
                    (int)(Rectangle.Y * yScale),
                    (int)(Rectangle.Width * xScale),
                    (int)(Rectangle.Height * yScale)
                );

                // Draw text twice for infinite loop effect
                float totalScrollWidth = textWidth + ScrollPadding;
                Vector2 pos1 = position + new Vector2(-scrollOffset, 0);
                Vector2 pos2 = position + new Vector2(-scrollOffset + totalScrollWidth, 0);

                spriteBatch.DrawString(font, text, pos1, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, text, pos2, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                // Restore previous scissor rectangle instead of resetting to Viewport.Bounds
                Main.instance.GraphicsDevice.ScissorRectangle = prevScissor;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.UIScaleMatrix);
            }
        }
    }
}
