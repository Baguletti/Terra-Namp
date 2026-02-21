using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.UI;
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

    private Dictionary<string, TextBanner> songBanners = new();
    private string lastSongsHash = "";

    private readonly ContextMenu contextMenu = new();

    public List<(string Title, string Uuid)> Songs { get; } = new();
    public string ActiveSongUuid { get; set; }

    public event Action<string> OnSongSelected;
    public event Action<string> OnSongDeleted;
    public event Action<string> OnSetAsBossMusic;
    public event Action<string> OnSetAsDeathMusic;

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

        string currentHash = string.Join("|", Songs.ConvertAll(s => s.Uuid));
        if (currentHash != lastSongsHash)
        {
            songBanners.Clear();
            for (int i = 0; i < Songs.Count; i++)
            {
                int textWidth = bounds.Width - 20;
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

            // Event assignment indicators
            var tStore = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            bool isBossTrack  = !string.IsNullOrEmpty(tStore.BossMusicUuid)  && tStore.BossMusicUuid  == Songs[i].Uuid;
            bool isDeathTrack = !string.IsNullOrEmpty(tStore.DeathMusicUuid) && tStore.DeathMusicUuid == Songs[i].Uuid;
            if (isBossTrack || isDeathTrack)
            {
                Rectangle marker = new(itemRect.Right - 6, itemRect.Y + 2, 3, ItemHeight - 4);
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, marker, accentColor * 0.85f);
            }

            Color titleColor = isActive ? accentColor : Color.White * 0.8f;
            Vector2 textPos = new(bounds.X + 8, y + (ItemHeight - font.MeasureString("A").Y * scale) / 2f);

            if (songBanners.TryGetValue(Songs[i].Uuid, out TextBanner banner))
            {
                int textWidth = bounds.Width - 20;
                banner.UpdateRectangle(new Rectangle(bounds.X + 8, y, textWidth, ItemHeight));
                banner.UpdateScrolling();
                banner.Draw(spriteBatch, textPos, titleColor);
            }

            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(bounds.X + 4, y + ItemHeight - 1, bounds.Width - 8, 1),
                Color.White * 0.05f);
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch); // handles scissor internally

        // Context menu drawn after scissor is restored
        Color accent = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;
        contextMenu.Draw(spriteBatch, accent);

        if (contextMenu.ContainsMouse())
            Main.LocalPlayer.mouseInterface = true;
    }

    public override void SafeClick(UIMouseEvent evt)
    {
        // Context menu has priority
        if (contextMenu.HandleLeftClick(Main.MouseScreen.ToPoint())) return;

        if (!IsScrollbarDragging && hoveredIndex >= 0 && hoveredIndex < Songs.Count)
            OnSongSelected?.Invoke(Songs[hoveredIndex].Uuid);
    }

    public override void SafeRightClick(UIMouseEvent evt)
    {
        // Dismiss context menu if already open
        if (contextMenu.IsVisible)
        {
            contextMenu.Hide();
            return;
        }

        if (hoveredIndex < 0 || hoveredIndex >= Songs.Count) return;

        string uuid = Songs[hoveredIndex].Uuid;

        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        string bossLabel  = store.BossMusicUuid  == uuid ? "Boss Music  [active]" : "Set as Boss Music";
        string deathLabel = store.DeathMusicUuid == uuid ? "Death Music [active]" : "Set as Death Music";

        contextMenu.Show(Main.MouseScreen.ToPoint(), new List<(string, Action)>
        {
            (bossLabel,  () => OnSetAsBossMusic?.Invoke(uuid)),
            (deathLabel, () => OnSetAsDeathMusic?.Invoke(uuid)),
            ("Delete",   () => { hoveredIndex = -1; OnSongDeleted?.Invoke(uuid); }),
        });
    }
}
