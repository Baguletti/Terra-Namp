using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terraria;
using Terraria.GameContent;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class ColorSwatch : SmartUIElement
{
    public Color SwatchColor { get; set; }
    public bool IsSelected { get; set; }

    public ColorSwatch(Color color)
    {
        SwatchColor = color;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();

        // Swatch fill
        Color drawColor = IsMouseHovering ? SwatchColor : SwatchColor * 0.8f;
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, drawColor);

        // Selection border
        if (IsSelected)
        {
            // Outer border
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X - 1, bounds.Y - 1, bounds.Width + 2, 1), Color.White);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X - 1, bounds.Y + bounds.Height, bounds.Width + 2, 1), Color.White);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X - 1, bounds.Y - 1, 1, bounds.Height + 2), Color.White);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + bounds.Width, bounds.Y - 1, 1, bounds.Height + 2), Color.White);
        }

        base.Draw(spriteBatch);
    }
}
