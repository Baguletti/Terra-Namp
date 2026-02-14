using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using Terraria;
using Terraria.GameContent;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class NowPlayingWidget : SmartUIElement
{
    private TextBanner titleBanner;
    private TextBanner authorBanner;
    private string lastTitle;
    private string lastAuthor;

    public string SongTitle { get; set; }
    public string SongAuthor { get; set; }

    public override void Draw(SpriteBatch spriteBatch)
    {
        var dims = GetDimensions();
        var font = FontAssets.MouseText.Value;
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        float scale = 0.75f;
        int lineH = (int)(font.MeasureString("A").Y * scale);

        string title = SongTitle ?? "---";
        string author = SongAuthor ?? "";

        int x = (int)dims.X;
        int y = (int)dims.Y;
        int width = (int)dims.Width;

        if (title != lastTitle)
        {
            Rectangle titleRect = new(x, y, width, lineH);
            titleBanner = new TextBanner(title, titleRect, font, scale);
            lastTitle = title;
        }

        if (author != lastAuthor)
        {
            Rectangle authorRect = new(x, y + lineH + 2, width, lineH);
            authorBanner = string.IsNullOrEmpty(author) ? null : new TextBanner(author, authorRect, font, scale);
            lastAuthor = author;
        }

        if (titleBanner != null)
        {
            titleBanner.UpdateRectangle(new Rectangle(x, y, width, lineH));
            titleBanner.UpdateScrolling();
            titleBanner.Draw(spriteBatch, new Vector2(x, y), store.PanelColor);
        }

        if (authorBanner != null)
        {
            authorBanner.UpdateRectangle(new Rectangle(x, y + lineH + 2, width, lineH));
            authorBanner.UpdateScrolling();
            authorBanner.Draw(spriteBatch, new Vector2(x, y + lineH + 2), store.SecondaryColor * 0.7f);
        }

        base.Draw(spriteBatch);
    }
}
