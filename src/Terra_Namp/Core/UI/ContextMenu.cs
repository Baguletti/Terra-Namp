using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Components;
using Terra_Namp.Core.IO;
using System;
using System.Collections.Generic;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Terra_Namp.Core.UI;

/// <summary>
/// Inline context menu drawn as an overlay in the parent's Draw() phase.
/// Pattern mirrors the emoji picker in SoundpadPanel — no UIElement, drawn after scissor is restored.
/// </summary>
public class ContextMenu
{
    private bool visible;
    private Point anchor;
    private List<(string Label, Action Callback)> items = new();
    private Rectangle bounds;

    private const int ItemHeight = 26;
    private const int PaddingX = 10;
    private const int PaddingV = 6;
    private const float Scale = 0.65f;
    private const int MinWidth = 130;

    public bool IsVisible => visible;

    public void Show(Point at, List<(string Label, Action Callback)> menuItems)
    {
        items = menuItems;
        anchor = at;
        visible = true;
        RecalculateBounds();
    }

    public void Hide() => visible = false;

    private void RecalculateBounds()
    {
        var font = FontAssets.MouseText.Value;
        int maxW = MinWidth;
        foreach (var (label, _) in items)
        {
            int w = (int)(font.MeasureString(label).X * Scale) + PaddingX * 2;
            if (w > maxW) maxW = w;
        }
        int height = items.Count * ItemHeight + PaddingV * 2;
        int x = anchor.X;
        int y = anchor.Y;
        if (x + maxW > Main.screenWidth) x = Main.screenWidth - maxW;
        if (y + height > Main.screenHeight) y = Main.screenHeight - height;
        bounds = new Rectangle(x, y, maxW, height);
    }

    /// <summary>
    /// Call from parent SafeClick. Returns true if click was consumed by the menu.
    /// Hides the menu when clicking outside.
    /// </summary>
    public bool HandleLeftClick(Point mouse)
    {
        if (!visible) return false;

        if (!bounds.Contains(mouse))
        {
            Hide();
            return false;
        }

        int relY = mouse.Y - bounds.Y - PaddingV;
        int index = relY / ItemHeight;
        if (index >= 0 && index < items.Count)
        {
            var callback = items[index].Callback;
            Hide();
            callback?.Invoke();
        }

        return true;
    }

    public bool ContainsMouse() => visible && bounds.Contains(Main.MouseScreen.ToPoint());

    /// <summary>
    /// Draw the context menu. Call AFTER base.Draw() so scissor is already restored.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Color accent)
    {
        if (!visible || items.Count == 0) return;

        RecalculateBounds();

        var font = FontAssets.MouseText.Value;
        Point mouse = Main.MouseScreen.ToPoint();

        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        int cr = store.CornerRadius;

        // Flush SpriteBatch before rendering — SDFRectangle.SampleVersion draws directly to the
        // GPU bypassing the SpriteBatch buffer. Without a flush, pending pad emoji/text draws
        // are submitted AFTER the SDF background, making them appear on top of the menu.
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

        // Dark accent-tinted background — fully opaque after flush so SDF ordering is correct
        Color bgColor = Color.Lerp(new Color(15, 15, 15), accent, 0.10f);
        bgColor.A = 255;
        DrawingUtils.DrawRoundedRect(spriteBatch, bounds, bgColor, cr);
        DrawingUtils.DrawRoundedBorder(spriteBatch, bounds, accent * 0.5f, cr);

        for (int i = 0; i < items.Count; i++)
        {
            int itemY = bounds.Y + PaddingV + i * ItemHeight;
            Rectangle itemRect = new(bounds.X + 2, itemY, bounds.Width - 4, ItemHeight);
            bool hovered = itemRect.Contains(mouse);

            if (hovered)
                DrawingUtils.DrawRoundedRect(spriteBatch, itemRect, accent * 0.25f, Math.Max(cr - 1, 0));

            float textH = font.MeasureString("A").Y * Scale;
            Vector2 textPos = new(bounds.X + PaddingX, itemY + (ItemHeight - textH) / 2f);
            spriteBatch.DrawString(font, items[i].Label, textPos,
                hovered ? accent : Color.White * 0.85f,
                0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
        }
    }
}
