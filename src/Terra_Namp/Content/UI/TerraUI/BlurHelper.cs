using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SilkyUIFramework;
using SilkyUIFramework.Graphics2D;
using Terraria;

namespace Terra_Namp.Content.UI.TerraUI;

public static class BlurHelper
{
    private static bool _warnedUnavailable;

    public static void DrawBlurredBackground(SpriteBatch spriteBatch, Rectangle screenArea, int blurLevel, float cornerRadius = 6f)
    {
        if (blurLevel <= 0) return;

        if (!BlurMakeSystem.BlurAvailable)
        {
            if (!_warnedUnavailable)
            {
                _warnedUnavailable = true;
                if (!BlurMakeSystem.EnableBlur)
                    Main.NewText("[Terra Namp] Blur disabled in SilkyUI config. Enable in Mod Configuration.", Color.Orange);
                else if (!Lighting.NotRetro)
                    Main.NewText("[Terra Namp] Blur requires non-Retro lighting mode. Change in Settings > Video.", Color.Orange);
            }
            return;
        }
        _warnedUnavailable = false;

        if (Main.gameMenu) return;

        var bt = BlurMakeSystem.BlurRenderTarget;
        if (bt == null || bt.IsDisposed) return;

        try
        {
            var scale = Main.UIScale;
            var position = new Vector2(screenArea.X, screenArea.Y) * scale;
            var size = new Vector2(screenArea.Width, screenArea.Height) * scale;
            var borderRadius = new Vector4(cornerRadius * scale);

            var device = Main.instance.GraphicsDevice;
            var screenSize = new Vector2(device.Viewport.Width, device.Viewport.Height);
            var texCoordPos = position / screenSize;
            var texCoordSize = size / screenSize;

            float alpha = Math.Clamp(blurLevel / 10f, 0.1f, 1f);
            SDFRectangle.SampleVersion(bt, position, size, texCoordPos, texCoordSize,
                borderRadius, Color.White * alpha, Matrix.Identity);
        }
        catch (Exception ex)
        {
            Main.NewText($"[Terra Namp] Blur error: {ex.GetType().Name}: {ex.Message}", Color.Red);
        }
    }

    public static void Unload()
    {
        _warnedUnavailable = false;
    }
}
