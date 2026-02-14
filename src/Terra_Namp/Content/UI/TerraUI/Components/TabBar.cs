using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class TabBar : SmartUIElement
{
    public const int TabBarHeight = 26;

    private readonly List<TabDefinition> tabs = new();
    private int activeIndex;

    public event Action<int> OnTabChanged;

    public int ActiveIndex
    {
        get => activeIndex;
        set
        {
            if (value == activeIndex) return;
            activeIndex = value;
            OnTabChanged?.Invoke(activeIndex);
        }
    }

    public string ActiveTabId => tabs.Count > 0 ? tabs[activeIndex].Id : "";

    public void AddTab(string id, string label)
    {
        tabs.Add(new TabDefinition { Id = id, Label = label });
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (tabs.Count == 0) return;

        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        Color accent = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;

        // Tab bar background
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, Color.Black * 0.2f);

        int tabWidth = bounds.Width / tabs.Count;

        for (int i = 0; i < tabs.Count; i++)
        {
            Rectangle tabRect = new(bounds.X + tabWidth * i, bounds.Y, tabWidth, bounds.Height);
            bool isActive = i == activeIndex;
            bool isHover = tabRect.Contains(Main.MouseScreen.ToPoint());

            // Tab background
            Color bgColor;
            if (isActive)
                bgColor = accent * 0.15f;
            else if (isHover)
                bgColor = Color.White * 0.08f;
            else
                bgColor = Color.Transparent;

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, tabRect, bgColor);

            // Active tab bottom line
            if (isActive)
            {
                spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                    new Rectangle(tabRect.X, tabRect.Y + tabRect.Height - 2, tabRect.Width, 2),
                    accent * 0.8f);
            }

            // Tab label
            float scale = 0.6f;
            Vector2 textSize = font.MeasureString(tabs[i].Label) * scale;
            Vector2 pos = new(
                tabRect.X + (tabRect.Width - textSize.X) / 2f,
                tabRect.Y + (tabRect.Height - textSize.Y) / 2f);

            Color textColor = isActive ? accent : (isHover ? Color.White * 0.9f : Color.White * 0.5f);
            spriteBatch.DrawString(font, tabs[i].Label, pos, textColor,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // Bottom separator line
        spriteBatch.Draw(TextureAssets.MagicPixel.Value,
            new Rectangle(bounds.X, bounds.Y + bounds.Height - 1, bounds.Width, 1),
            accent * 0.15f);

        base.Draw(spriteBatch);
    }

    public override void SafeClick(Terraria.UI.UIMouseEvent evt)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        int tabWidth = bounds.Width / tabs.Count;
        int relativeX = (int)(Main.MouseScreen.X - bounds.X);
        int clickedIndex = relativeX / tabWidth;

        if (clickedIndex >= 0 && clickedIndex < tabs.Count)
            ActiveIndex = clickedIndex;
    }

    private struct TabDefinition
    {
        public string Id;
        public string Label;
    }
}
