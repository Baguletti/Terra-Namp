using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using Terra_Namp.Networking;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class AdminPanel : ScrollablePanel
{
    private const int ItemHeight = 32;
    private const int BtnSize = 24;
    private const int BtnGap = 4;
    private const int Padding = 10;
    private const int CornerRadius = 4;

    private Texture2D allowIcon;
    private Texture2D declineIcon;
    private Texture2D userStarIcon;

    private List<PlayerEntry> players = new();
    private int hoveredIndex = -1;
    private HitArea hoveredArea = HitArea.None;
    private float glowTimer;

    private enum HitArea { None, Access, Admin }

    private struct PlayerEntry
    {
        public int PlayerIndex;
        public string Name;
        public PermissionRole Role;
    }

    protected override int GetTotalContentHeight() => players.Count * ItemHeight;

    public override void OnInitialize()
    {
        allowIcon = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/Allow", AssetRequestMode.ImmediateLoad).Value;
        declineIcon = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/Decline", AssetRequestMode.ImmediateLoad).Value;
        userStarIcon = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/User_star", AssetRequestMode.ImmediateLoad).Value;
        base.OnInitialize();
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        base.SafeUpdate(gameTime);
        glowTimer += 0.03f;
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        players.Clear();

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (Main.player[i] == null || !Main.player[i].active)
                continue;

            string name = Main.player[i].name;
            if (i == Main.myPlayer)
                name += " (You)";

            PlayerPermissions perms = ClientPermissionCache.Permissions.TryGetValue(i, out var p)
                ? p
                : PlayerPermissions.Default;

            players.Add(new PlayerEntry
            {
                PlayerIndex = i,
                Name = name,
                Role = perms.Role,
            });
        }

        // Sort: admins first, then controllers, then listeners
        players.Sort((a, b) =>
        {
            int roleCompare = b.Role.CompareTo(a.Role);
            if (roleCompare != 0) return roleCompare;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });
    }

    protected override void DrawScrollContent(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        Color accentColor = store.PanelColor;
        float textScale = 0.7f;

        if (players.Count == 0)
        {
            string noPlayers = "No players";
            Vector2 textSize = font.MeasureString(noPlayers) * textScale;
            spriteBatch.DrawString(font, noPlayers,
                new Vector2(bounds.X + (bounds.Width - textSize.X) / 2f, bounds.Y + (bounds.Height - textSize.Y) / 2f),
                Color.White * 0.3f, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            return;
        }

        hoveredIndex = -1;
        hoveredArea = HitArea.None;

        for (int i = 0; i < players.Count; i++)
        {
            int y = bounds.Y + (int)(i * ItemHeight - ScrollOffset);
            if (y + ItemHeight <= bounds.Y || y >= bounds.Y + bounds.Height)
                continue;

            var entry = players[i];
            Rectangle itemRect = new(bounds.X, y, bounds.Width, ItemHeight);
            bool isHovered = itemRect.Contains(Main.MouseScreen.ToPoint()) && bounds.Contains(Main.MouseScreen.ToPoint());

            // Button positions (right-aligned): [Access] [Admin/Star]
            int adminBtnX = bounds.X + bounds.Width - Padding - BtnSize;
            int accessBtnX = adminBtnX - BtnSize - BtnGap;
            int btnY = y + (ItemHeight - BtnSize) / 2;

            Rectangle accessRect = new(accessBtnX, btnY, BtnSize, BtnSize);
            Rectangle adminRect = new(adminBtnX, btnY, BtnSize, BtnSize);

            // No buttons for self or super user
            bool isSelf = entry.PlayerIndex == Main.myPlayer;
            bool isSuperUser = ClientPermissionCache.IsSuperUser(entry.PlayerIndex);
            bool noButtons = isSelf || isSuperUser;
            if (isHovered)
            {
                hoveredIndex = i;
                if (!noButtons && accessRect.Contains(Main.MouseScreen.ToPoint()))
                    hoveredArea = HitArea.Access;
                else if (!noButtons && adminRect.Contains(Main.MouseScreen.ToPoint()))
                    hoveredArea = HitArea.Admin;
                else
                    hoveredArea = HitArea.None;
            }

            // Row background
            if (isHovered)
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, itemRect, Color.White * 0.07f);

            // Player name color
            Color nameColor = entry.Role == PermissionRole.Admin ? accentColor :
                              entry.Role == PermissionRole.Listener ? Color.White * 0.5f :
                              Color.White * 0.8f;
            float nameY = y + (ItemHeight - font.MeasureString("A").Y * textScale) / 2f;
            int maxNameWidth = accessBtnX - bounds.X - Padding - BtnGap;

            // Clip name text if too long
            string displayName = entry.Name;
            while (font.MeasureString(displayName).X * textScale > maxNameWidth && displayName.Length > 3)
                displayName = displayName[..^4] + "...";

            spriteBatch.DrawString(font, displayName,
                new Vector2(bounds.X + Padding, nameY),
                nameColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

            if (!noButtons)
            {
                // --- Access button: Allow (Controller+) / Decline (Listener) ---
                bool canPlay = entry.Role >= PermissionRole.Controller;
                bool accessHovered = isHovered && hoveredArea == HitArea.Access;
                DrawAccessButton(spriteBatch, accessRect, canPlay, accessHovered, accentColor);

                // --- Admin button (Star): toggle Admin on/off ---
                bool isAdmin = entry.Role == PermissionRole.Admin;
                bool adminHovered = isHovered && hoveredArea == HitArea.Admin;
                DrawAdminButton(spriteBatch, adminRect, isAdmin, adminHovered, accentColor);
            }
            else
            {
                // Show role label (no buttons)
                string roleLabel = isSuperUser ? "Super Admin" : entry.Role switch
                {
                    PermissionRole.Admin => "Admin",
                    PermissionRole.Controller => "Controller",
                    _ => "Listener",
                };
                float labelScale = 0.6f;
                Vector2 labelSize = font.MeasureString(roleLabel) * labelScale;
                float labelX = bounds.X + bounds.Width - Padding - labelSize.X;
                float labelY2 = y + (ItemHeight - labelSize.Y) / 2f;
                spriteBatch.DrawString(font, roleLabel,
                    new Vector2(labelX, labelY2),
                    accentColor * 0.6f, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
            }

            // Separator
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(bounds.X + 4, y + ItemHeight - 1, bounds.Width - 8, 1),
                Color.White * 0.05f);
        }

        // Tooltip
        if (hoveredIndex >= 0 && hoveredIndex < players.Count && hoveredArea != HitArea.None)
        {
            string tooltip = GetTooltip(players[hoveredIndex], hoveredArea);
            if (!string.IsNullOrEmpty(tooltip))
                Main.instance.MouseText(tooltip);
        }
    }

    private void DrawAccessButton(SpriteBatch sb, Rectangle rect, bool canPlay, bool isHovered, Color accentColor)
    {
        Color bgColor = isHovered ? Color.White * 0.15f : Color.White * 0.05f;
        DrawingUtils.DrawRoundedRect(sb, rect, bgColor, CornerRadius);

        if (isHovered)
            DrawingUtils.DrawRoundedBorder(sb, rect, accentColor * 0.4f, CornerRadius);

        Texture2D icon = canPlay ? allowIcon : declineIcon;
        Color iconColor = canPlay ? Color.Green * 0.9f : Color.Red * 0.7f;

        int iconPad = 4;
        int iconSize = Math.Min(rect.Width - iconPad * 2, rect.Height - iconPad * 2);
        Rectangle iconRect = new(
            rect.X + (rect.Width - iconSize) / 2,
            rect.Y + (rect.Height - iconSize) / 2,
            iconSize, iconSize);

        sb.Draw(icon, iconRect, iconColor);
    }

    private void DrawAdminButton(SpriteBatch sb, Rectangle rect, bool isAdmin, bool isHovered, Color accentColor)
    {
        // Glow effect for Admin
        if (isAdmin)
        {
            float glowAlpha = 0.15f + 0.1f * MathF.Sin(glowTimer);
            int glowPad = 3;
            Rectangle glowRect = new(rect.X - glowPad, rect.Y - glowPad,
                rect.Width + glowPad * 2, rect.Height + glowPad * 2);
            DrawingUtils.DrawRoundedRect(sb, glowRect, accentColor * glowAlpha, CornerRadius + 2);
        }

        Color bgColor = isAdmin
            ? accentColor * 0.25f
            : isHovered ? Color.White * 0.15f : Color.White * 0.05f;
        DrawingUtils.DrawRoundedRect(sb, rect, bgColor, CornerRadius);

        if (isHovered || isAdmin)
            DrawingUtils.DrawRoundedBorder(sb, rect, accentColor * 0.4f, CornerRadius);

        Color iconColor = isAdmin ? accentColor : Color.White * 0.3f;

        int iconPad = 4;
        int iconSize = Math.Min(rect.Width - iconPad * 2, rect.Height - iconPad * 2);
        Rectangle iconRect = new(
            rect.X + (rect.Width - iconSize) / 2,
            rect.Y + (rect.Height - iconSize) / 2,
            iconSize, iconSize);

        sb.Draw(userStarIcon, iconRect, iconColor);
    }

    private static string GetTooltip(PlayerEntry entry, HitArea area)
    {
        if (area == HitArea.Access)
        {
            bool canPlay = entry.Role >= PermissionRole.Controller;
            return canPlay
                ? "Controller — can control playback (click to revoke)"
                : "Listener — can only hear music (click to allow control)";
        }

        if (area == HitArea.Admin)
        {
            return entry.Role == PermissionRole.Admin
                ? "Admin — can manage permissions (click to revoke)"
                : "Not admin (click to grant admin)";
        }

        return "";
    }

    public override void SafeClick(UIMouseEvent evt)
    {
        if (IsScrollbarDragging || hoveredIndex < 0 || hoveredIndex >= players.Count)
            return;

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        var entry = players[hoveredIndex];

        // Can't modify own permissions or super user
        if (entry.PlayerIndex == Main.myPlayer || ClientPermissionCache.IsSuperUser(entry.PlayerIndex))
            return;

        if (hoveredArea == HitArea.Access)
        {
            // Toggle Controller <-> Listener
            // If Admin and clicking Decline -> demote to Listener
            PermissionRole newRole = entry.Role >= PermissionRole.Controller
                ? PermissionRole.Listener
                : PermissionRole.Controller;

            PacketBuilder.PermissionUpdate(
                (byte)Main.myPlayer,
                (byte)entry.PlayerIndex,
                newRole
            ).Send();
        }
        else if (hoveredArea == HitArea.Admin)
        {
            // Toggle Admin on/off
            // Admin -> Controller, non-Admin -> Admin
            PermissionRole newRole = entry.Role == PermissionRole.Admin
                ? PermissionRole.Controller
                : PermissionRole.Admin;

            PacketBuilder.PermissionUpdate(
                (byte)Main.myPlayer,
                (byte)entry.PlayerIndex,
                newRole
            ).Send();
        }
    }
}
