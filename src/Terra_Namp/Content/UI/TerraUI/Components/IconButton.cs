using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using Terraria;
using Terraria.ModLoader;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class IconButton : SmartUIElement
{
    private const int CornerRadius = 4;
    private const int DefaultIconPadding = 6; // Reduced for 20% larger icons

    private readonly string iconPath;
    private readonly int iconPadding;
    private readonly PressAnimator pressAnim = new();
    private Texture2D iconTexture;

    public bool IsActive { get; set; }

    public IconButton(string iconPath, int iconPadding = DefaultIconPadding)
    {
        this.iconPath = iconPath;
        this.iconPadding = iconPadding;
    }

    public override void OnInitialize()
    {
        // Load texture
        iconTexture = ModContent.Request<Texture2D>(iconPath, AssetRequestMode.ImmediateLoad).Value;
        base.OnInitialize();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        pressAnim.Update(IsMouseHovering && Main.mouseLeft);
        Rectangle bounds = pressAnim.GetAnimatedBounds(GetDimensions().ToRectangle());
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();

        // Background (highlighted if active)
        Color bgColor = IsActive ? store.PanelColor * 0.2f :
                        IsMouseHovering ? Color.White * 0.12f : Color.White * 0.05f;
        DrawingUtils.DrawRoundedRect(spriteBatch, bounds, bgColor, CornerRadius);

        // Border (accent color when active or hovering)
        if (IsActive || IsMouseHovering)
            DrawingUtils.DrawRoundedBorder(spriteBatch, bounds, store.PanelColor * 0.4f, CornerRadius);

        // Icon (centered, tinted with accent when active)
        if (iconTexture != null)
        {
            Color iconColor = IsActive ? store.PanelColor :
                             IsMouseHovering ? store.PanelColor : Color.White * 0.8f;

            // Center the icon
            int iconSize = Math.Min(bounds.Width - iconPadding * 2, bounds.Height - iconPadding * 2);
            Rectangle iconRect = new(
                bounds.X + (bounds.Width - iconSize) / 2,
                bounds.Y + (bounds.Height - iconSize) / 2,
                iconSize,
                iconSize
            );

            spriteBatch.Draw(iconTexture, iconRect, iconColor);
        }

        base.Draw(spriteBatch);
    }
}
