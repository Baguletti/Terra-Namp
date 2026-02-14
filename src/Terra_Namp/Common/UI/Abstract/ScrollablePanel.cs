using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace Terra_Namp.Common.UI.Abstract;

public abstract class ScrollablePanel : SmartUIElement
{
    private const int ScrollbarWidth = 3;
    private const int ScrollbarWidthHover = 6;
    private const int ScrollbarHitArea = 12;

    private float scrollOffset;
    private bool scrollbarDragging;
    private float dragOffsetY;
    private bool scrollbarHovered;

    protected float ScrollOffset
    {
        get => scrollOffset;
        set => scrollOffset = value;
    }

    protected bool IsScrollbarDragging => scrollbarDragging;

    protected bool EnableGridSnap { get; set; }
    protected int GridSnapSize { get; set; } = 1;

    protected abstract int GetTotalContentHeight();

    protected virtual void DrawScrollContent(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();

        // Scissor clip
        RasterizerState rState = new() { ScissorTestEnable = true, CullMode = CullMode.None };
        var prevScissor = Main.instance.GraphicsDevice.ScissorRectangle;

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, rState, null, Main.UIScaleMatrix);

        float uiScaleX = Main.UIScaleMatrix.M11;
        float uiScaleY = Main.UIScaleMatrix.M22;
        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(
            (int)(bounds.X * uiScaleX), (int)(bounds.Y * uiScaleY),
            (int)(bounds.Width * uiScaleX), (int)(bounds.Height * uiScaleY));

        DrawScrollContent(spriteBatch);

        Main.instance.GraphicsDevice.ScissorRectangle = prevScissor;
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

        // Scrollbar
        int totalHeight = GetTotalContentHeight();
        float maxScroll = Math.Max(0, totalHeight - bounds.Height);
        if (maxScroll > 0)
        {
            float visibleRatio = bounds.Height / (float)totalHeight;
            int scrollbarHeight = Math.Max(16, (int)(bounds.Height * visibleRatio));
            float scrollRatio = scrollOffset / maxScroll;
            int scrollbarY = bounds.Y + (int)(scrollRatio * (bounds.Height - scrollbarHeight));

            Rectangle scrollbarHitRect = new(bounds.X + bounds.Width - ScrollbarHitArea, bounds.Y, ScrollbarHitArea, bounds.Height);
            scrollbarHovered = scrollbarHitRect.Contains(Main.MouseScreen.ToPoint()) || scrollbarDragging;

            int barW = scrollbarHovered ? ScrollbarWidthHover : ScrollbarWidth;
            Color barColor = scrollbarHovered ? Color.White * 0.4f : Color.White * 0.2f;

            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(bounds.X + bounds.Width - barW, scrollbarY, barW, scrollbarHeight),
                barColor);
        }
    }

    public override void SafeMouseDown(UIMouseEvent evt)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        int totalHeight = GetTotalContentHeight();
        float maxScroll = totalHeight - bounds.Height;
        if (maxScroll <= 0) return;

        Rectangle scrollbarHitRect = new(bounds.X + bounds.Width - ScrollbarHitArea, bounds.Y, ScrollbarHitArea, bounds.Height);
        if (!scrollbarHitRect.Contains(Main.MouseScreen.ToPoint())) return;

        float visibleRatio = bounds.Height / (float)totalHeight;
        int scrollbarHeight = Math.Max(16, (int)(bounds.Height * visibleRatio));
        float scrollRatio = scrollOffset / maxScroll;
        int scrollbarY = bounds.Y + (int)(scrollRatio * (bounds.Height - scrollbarHeight));

        Rectangle thumbRect = new(bounds.X + bounds.Width - ScrollbarHitArea, scrollbarY, ScrollbarHitArea, scrollbarHeight);

        if (thumbRect.Contains(Main.MouseScreen.ToPoint()))
        {
            scrollbarDragging = true;
            dragOffsetY = Main.MouseScreen.Y - scrollbarY;
        }
        else
        {
            scrollbarDragging = true;
            dragOffsetY = scrollbarHeight / 2f;
            UpdateScrollFromMouse(bounds, scrollbarHeight, maxScroll);
            Recalculate();
        }
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            PlayerInput.LockVanillaMouseScroll("Terra_Namp: Scroll");
        }

        if (scrollbarDragging)
        {
            if (!Main.mouseLeft)
            {
                scrollbarDragging = false;
                ClampScroll();
            }
            else
            {
                Rectangle bounds = GetDimensions().ToRectangle();
                int totalHeight = GetTotalContentHeight();
                float maxScroll = Math.Max(0, totalHeight - bounds.Height);
                float visibleRatio = bounds.Height / (float)totalHeight;
                int scrollbarHeight = Math.Max(16, (int)(bounds.Height * visibleRatio));
                UpdateScrollFromMouse(bounds, scrollbarHeight, maxScroll);
                Recalculate();
            }
        }
    }

    public override void SafeScrollWheel(UIScrollWheelEvent evt)
    {
        Main.LocalPlayer.mouseInterface = true;
        PlayerInput.LockVanillaMouseScroll("Terra_Namp: Scroll");

        scrollOffset -= evt.ScrollWheelValue * 0.5f;
        ClampScroll();
        Recalculate();
    }

    private void UpdateScrollFromMouse(Rectangle bounds, int scrollbarHeight, float maxScroll)
    {
        float trackHeight = bounds.Height - scrollbarHeight;
        if (trackHeight <= 0) return;

        float mouseRelY = Main.MouseScreen.Y - dragOffsetY - bounds.Y;
        float ratio = MathHelper.Clamp(mouseRelY / trackHeight, 0f, 1f);
        scrollOffset = ratio * maxScroll;
    }

    protected void ClampScroll()
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        float maxScroll = Math.Max(0, GetTotalContentHeight() - bounds.Height);
        scrollOffset = MathHelper.Clamp(scrollOffset, 0f, maxScroll);

        if (EnableGridSnap && GridSnapSize > 0)
            scrollOffset = MathF.Floor(scrollOffset / GridSnapSize) * GridSnapSize;
    }
}
