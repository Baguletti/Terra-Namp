using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SilkyUIFramework.Graphics2D;
using Terraria;
using Terraria.GameContent;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public static class DrawingUtils
{
    /// <summary>
    /// Draws a rounded rectangle using SilkyUI's SDF rendering (fast, smooth, shader-based)
    /// </summary>
    public static void DrawRoundedRect(SpriteBatch sb, Rectangle rect, Color color, int cornerRadius)
    {
        if (cornerRadius <= 0)
        {
            sb.Draw(TextureAssets.MagicPixel.Value, rect, color);
            return;
        }

        var scale = Main.UIScale;
        var position = new Vector2(rect.X, rect.Y) * scale;
        var size = new Vector2(rect.Width, rect.Height) * scale;
        var borderRadius = new Vector4(cornerRadius * scale);

        // Use SampleVersion with white pixel texture
        // UV coords (0,0)-(1,1) to fill entire texture
        SDFRectangle.SampleVersion(TextureAssets.MagicPixel.Value, position, size,
            Vector2.Zero, Vector2.One, borderRadius, color, Matrix.Identity);
    }

    /// <summary>
    /// Draws a rounded rectangle with individual corner radii (topLeft, topRight, bottomRight, bottomLeft)
    /// </summary>
    public static void DrawRoundedRect(SpriteBatch sb, Rectangle rect, Color color, Vector4 cornerRadii)
    {
        var scale = Main.UIScale;
        var position = new Vector2(rect.X, rect.Y) * scale;
        var size = new Vector2(rect.Width, rect.Height) * scale;
        var borderRadius = cornerRadii * scale;

        SDFRectangle.SampleVersion(TextureAssets.MagicPixel.Value, position, size,
            Vector2.Zero, Vector2.One, borderRadius, color, Matrix.Identity);
    }

    /// <summary>
    /// Draws a rounded rectangle border using SilkyUI's SDF rendering
    /// Note: For thin borders (1-2px), draws a semi-transparent filled rectangle for performance
    /// </summary>
    public static void DrawRoundedBorder(SpriteBatch sb, Rectangle rect, Color color, int cornerRadius, int borderWidth = 1)
    {
        if (cornerRadius <= 0)
        {
            // Draw simple border without corners
            var pixel = TextureAssets.MagicPixel.Value;
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, borderWidth), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - borderWidth, rect.Width, borderWidth), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, borderWidth, rect.Height), color);
            sb.Draw(pixel, new Rectangle(rect.X + rect.Width - borderWidth, rect.Y, borderWidth, rect.Height), color);
            return;
        }

        // For thin borders, just draw a very transparent filled rounded rect
        // This is much faster than true border rendering and looks good for subtle accents
        var scale = Main.UIScale;
        var position = new Vector2(rect.X, rect.Y) * scale;
        var size = new Vector2(rect.Width, rect.Height) * scale;
        var borderRadius = new Vector4(cornerRadius * scale);

        // Reduce alpha to create border-like appearance
        Color borderColor = color * 0.3f;

        SDFRectangle.SampleVersion(TextureAssets.MagicPixel.Value, position, size,
            Vector2.Zero, Vector2.One, borderRadius, borderColor, Matrix.Identity);
    }
}
