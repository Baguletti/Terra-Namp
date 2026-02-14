using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Enums;
using Terra_Namp.Core.IO;
using Terraria;
using Terraria.ModLoader;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class IconToggleButton : SmartUIElement
{
    private const int CornerRadius = 4;
    private const int DefaultIconPadding = 6; // Reduced for 20% larger icons

    private readonly int iconPadding;
    private readonly PressAnimator pressAnim = new();
    private Texture2D nextIcon;
    private Texture2D shuffleIcon;
    private Texture2D loopIcon;

    public PlayMode CurrentMode { get; set; }

    public IconToggleButton(int iconPadding = DefaultIconPadding)
    {
        this.iconPadding = iconPadding;
    }

    public override void OnInitialize()
    {
        // Load all mode icons
        nextIcon = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/NextTrack", AssetRequestMode.ImmediateLoad).Value;
        shuffleIcon = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/Shuffle", AssetRequestMode.ImmediateLoad).Value;
        loopIcon = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/Loop", AssetRequestMode.ImmediateLoad).Value;
        base.OnInitialize();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        pressAnim.Update(IsMouseHovering && Main.mouseLeft);
        Rectangle bounds = pressAnim.GetAnimatedBounds(GetDimensions().ToRectangle());
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();

        // Background
        Color bgColor = IsMouseHovering ? Color.White * 0.12f : Color.White * 0.05f;
        DrawingUtils.DrawRoundedRect(spriteBatch, bounds, bgColor, CornerRadius);

        // Border on hover
        if (IsMouseHovering)
            DrawingUtils.DrawRoundedBorder(spriteBatch, bounds, store.PanelColor * 0.25f, CornerRadius);

        // Select icon based on mode
        Texture2D currentIcon = CurrentMode switch
        {
            PlayMode.Next => nextIcon,
            PlayMode.Shuffle => shuffleIcon,
            PlayMode.Loop => loopIcon,
            _ => nextIcon
        };

        // Icon (centered, tinted)
        if (currentIcon != null)
        {
            Color iconColor = IsMouseHovering ? store.PanelColor : Color.White * 0.8f;

            // Center the icon
            int iconSize = Math.Min(bounds.Width - iconPadding * 2, bounds.Height - iconPadding * 2);
            Rectangle iconRect = new(
                bounds.X + (bounds.Width - iconSize) / 2,
                bounds.Y + (bounds.Height - iconSize) / 2,
                iconSize,
                iconSize
            );

            spriteBatch.Draw(currentIcon, iconRect, iconColor);
        }

        base.Draw(spriteBatch);
    }
}
