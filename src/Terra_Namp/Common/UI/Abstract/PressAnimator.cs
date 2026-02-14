using Microsoft.Xna.Framework;

namespace Terra_Namp.Common.UI.Abstract;

public class PressAnimator
{
    private const float PressedScale = 0.92f;
    private const float LerpIn = 0.5f;
    private const float LerpOut = 0.3f;

    private float currentScale = 1f;

    public void Update(bool isPressed)
    {
        float target = isPressed ? PressedScale : 1f;
        float speed = isPressed ? LerpIn : LerpOut;
        currentScale = MathHelper.Lerp(currentScale, target, speed);
    }

    public Rectangle GetAnimatedBounds(Rectangle bounds)
    {
        if (currentScale >= 0.999f)
            return bounds;

        int newW = (int)(bounds.Width * currentScale);
        int newH = (int)(bounds.Height * currentScale);
        int offsetX = (bounds.Width - newW) / 2;
        int offsetY = (bounds.Height - newH) / 2;

        return new Rectangle(bounds.X + offsetX, bounds.Y + offsetY, newW, newH);
    }
}
