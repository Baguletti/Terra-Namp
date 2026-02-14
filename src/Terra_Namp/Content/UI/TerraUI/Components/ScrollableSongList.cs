using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.IO;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class ScrollableSongList : ScrollablePanel
{
    private const int ItemHeight = 28;

    private int hoveredIndex = -1;

    // Scrolling text for song titles
    private Dictionary<string, TextBanner> songBanners = new();
    private string lastSongsHash = "";

    public List<(string Title, string Uuid)> Songs { get; } = new();
    public string ActiveSongUuid { get; set; }

    public event Action<string> OnSongSelected;
    public event Action<string> OnSongDeleted;

    public ScrollableSongList()
    {
        EnableGridSnap = true;
        GridSnapSize = ItemHeight;
    }

    protected override int GetTotalContentHeight() => Songs.Count * ItemHeight;

    protected override void DrawScrollContent(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        Color accentColor = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;
        float scale = 0.7f;

        if (Songs.Count == 0)
        {
            string noSongs = "No songs yet";
            Vector2 textSize = font.MeasureString(noSongs) * scale;
            spriteBatch.DrawString(font, noSongs,
                new Vector2(bounds.X + (bounds.Width - textSize.X) / 2f, bounds.Y + (bounds.Height - textSize.Y) / 2f),
                Color.White * 0.3f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return;
        }

        // Create TextBanners if song list changed
        string currentHash = string.Join("|", Songs.ConvertAll(s => s.Uuid));
        if (currentHash != lastSongsHash)
        {
            songBanners.Clear();
            for (int i = 0; i < Songs.Count; i++)
            {
                int textWidth = bounds.Width - 20; // Leave space for scrollbar
                Rectangle textRect = new(bounds.X + 8, 0, textWidth, ItemHeight);
                songBanners[Songs[i].Uuid] = new TextBanner(Songs[i].Title, textRect, font, scale);
            }
            lastSongsHash = currentHash;
        }

        hoveredIndex = -1;

        for (int i = 0; i < Songs.Count; i++)
        {
            int y = bounds.Y + (int)(i * ItemHeight - ScrollOffset);
            if (y + ItemHeight <= bounds.Y || y >= bounds.Y + bounds.Height)
                continue;

            Rectangle itemRect = new(bounds.X, y, bounds.Width, ItemHeight);
            bool isActive = Songs[i].Uuid == ActiveSongUuid;
            bool isHovered = itemRect.Contains(Main.MouseScreen.ToPoint()) && bounds.Contains(Main.MouseScreen.ToPoint());

            if (isHovered) hoveredIndex = i;

            if (isActive)
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, itemRect, accentColor * 0.15f);
            else if (isHovered)
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, itemRect, Color.White * 0.07f);

            // Update and draw scrolling text
            Color titleColor = isActive ? accentColor : Color.White * 0.8f;
            Vector2 textPos = new(bounds.X + 8, y + (ItemHeight - font.MeasureString("A").Y * scale) / 2f);

            if (songBanners.TryGetValue(Songs[i].Uuid, out TextBanner banner))
            {
                // Update rectangle to current Y position for proper scissor clipping
                int textWidth = bounds.Width - 20;
                banner.UpdateRectangle(new Rectangle(bounds.X + 8, y, textWidth, ItemHeight));
                banner.UpdateScrolling();
                banner.Draw(spriteBatch, textPos, titleColor);
            }

            // Separator
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(bounds.X + 4, y + ItemHeight - 1, bounds.Width - 8, 1),
                Color.White * 0.05f);
        }
    }

    public override void SafeClick(UIMouseEvent evt)
    {
        if (!IsScrollbarDragging && hoveredIndex >= 0 && hoveredIndex < Songs.Count)
            OnSongSelected?.Invoke(Songs[hoveredIndex].Uuid);
    }

    public bool AllowDelete { get; set; } = true;

    public override void SafeRightClick(UIMouseEvent evt)
    {
        if (!AllowDelete) return;

        if (hoveredIndex >= 0 && hoveredIndex < Songs.Count)
        {
            string uuidToDelete = Songs[hoveredIndex].Uuid;
            hoveredIndex = -1; // Reset immediately to prevent accessing deleted item
            OnSongDeleted?.Invoke(uuidToDelete);
        }
    }
}
