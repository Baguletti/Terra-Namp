using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.Audio;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Components;
using Terra_Namp.Core.IO;
using Terraria;
using Terraria.ModLoader;

namespace Terra_Namp.Content.UI.TerraUI;

public class MiniPlayerPanel : DraggableUIElement
{
    public const int MiniWidth = TerraMainPanel.PanelWidth;
    public const int MiniHeight = 96;
    private const int TopRowHeight = 22;
    private const int Padding = 10;

    private PlayPauseButton playPauseButton;
    private NowPlayingWidget nowPlaying;
    private Visualizer visualizer;

    public TerraMainPanel FullPanel { get; set; }

    public override Rectangle DragBox
    {
        get
        {
            var dims = GetDimensions();
            return new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, TopRowHeight);
        }
    }

    public override Vector2 DefaultPosition
    {
        get
        {
            var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            return new Vector2(store.WindowPositionX, store.WindowPositionY);
        }
    }

    public override void SafeOnInitialize()
    {
        int contentWidth = MiniWidth - Padding * 2;

        // Title row (title only, no author)
        nowPlaying = new NowPlayingWidget();
        nowPlaying.Left.Set(Padding, 0);
        nowPlaying.Top.Set(2, 0);
        nowPlaying.Width.Set(contentWidth, 0);
        nowPlaying.Height.Set(20, 0);
        Append(nowPlaying);

        // Visualizer - taller now, takes up more space
        int visY = TopRowHeight;
        int visHeight = 40; // Increased from ~20 to 40
        visualizer = new Visualizer();
        visualizer.Left.Set(Padding, 0);
        visualizer.Top.Set(visY, 0);
        visualizer.Width.Set(contentWidth, 0);
        visualizer.Height.Set(visHeight, 0);
        Append(visualizer);

        // Control buttons below visualizer
        int btnW = 28;
        int btnH = 24;
        int gap = 6;
        int totalBtnsWidth = 5 * btnW + 4 * gap;
        int startX = Padding + (contentWidth - totalBtnsWidth) / 2;
        int btnY = visY + visHeight + 2;

        string[] iconPaths =
        {
            "Terra_Namp/Assets/UI/Icons/Previous",
            "Terra_Namp/Assets/UI/Icons/Rewind",
            null, // PlayPauseButton
            "Terra_Namp/Assets/UI/Icons/Forward",
            "Terra_Namp/Assets/UI/Icons/Next"
        };

        Action[] actions =
        {
            () =>
            {
                if (FullPanel?.ActiveSong != null
                    && !ModContent.GetInstance<TerraTrackUpdaterSystem>().CurrentlyForcingSong)
                {
                    string prev = FullPanel.ActiveSong.GetPreviousSongUuid();
                    FullPanel.StopCurrentSong();
                    FullPanel.BeginPlayingSong(prev);
                }
            },
            () => FullPanel?.ActiveSong?.Skip(-10),
            () => FullPanel?.ActiveSong?.Toggle(),
            () => FullPanel?.ActiveSong?.Skip(10),
            () =>
            {
                if (FullPanel?.ActiveSong != null
                    && !ModContent.GetInstance<TerraTrackUpdaterSystem>().CurrentlyForcingSong)
                {
                    string next = FullPanel.ActiveSong.GetNextSongUuid();
                    FullPanel.StopCurrentSong();
                    FullPanel.BeginPlayingSong(next);
                }
            },
        };

        int x = startX;
        for (int i = 0; i < iconPaths.Length; i++)
        {
            int idx = i;

            if (i == 2)
            {
                playPauseButton = new PlayPauseButton(iconPadding: 6);
                playPauseButton.Left.Set(x, 0);
                playPauseButton.Top.Set(btnY, 0);
                playPauseButton.Width.Set(btnW, 0);
                playPauseButton.Height.Set(btnH, 0);
                playPauseButton.OnLeftClick += (evt, args) => actions[idx]();
                Append(playPauseButton);
            }
            else
            {
                var btn = new IconButton(iconPaths[i], iconPadding: 4);
                btn.Left.Set(x, 0);
                btn.Top.Set(btnY, 0);
                btn.Width.Set(btnW, 0);
                btn.Height.Set(btnH, 0);
                btn.OnLeftClick += (evt, args) => actions[idx]();
                Append(btn);
            }

            x += btnW + gap;
        }
    }

    public override void DraggableDraw(SpriteBatch spriteBatch)
    {
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        Rectangle drawBox = GetDimensions().ToRectangle();
        Color accentColor = store.PanelColor;
        Color backgroundColor = store.PanelBackgroundColor;
        float opacity = store.PanelOpacity;
        int cornerRadius = store.CornerRadius;

        if (store.BlurLevel > 0)
            BlurHelper.DrawBlurredBackground(spriteBatch, drawBox, store.BlurLevel, cornerRadius);

        DrawingUtils.DrawRoundedRect(spriteBatch, drawBox, backgroundColor * opacity, cornerRadius);
        DrawingUtils.DrawRoundedBorder(spriteBatch, drawBox, accentColor * 0.3f, cornerRadius);
    }

    public override void DraggableUpdate(GameTime gameTime)
    {
        var song = FullPanel?.ActiveSong;

        if (playPauseButton != null)
            playPauseButton.IsPlaying = song?.IsPlaying ?? false;

        if (nowPlaying != null)
        {
            nowPlaying.SongTitle = song?.Name ?? "---";
            nowPlaying.SongAuthor = "";
        }

        if (visualizer != null)
            visualizer.AudioData = song?.BufferToSubmit;
    }

    protected override void OnDragEnd(Vector2 position)
    {
        var dims = GetDimensions();
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        store.WindowPositionX = (position.X + dims.Width / 2f) / Main.screenWidth;
        store.WindowPositionY = (position.Y + dims.Height / 2f) / Main.screenHeight;
        store.ForceSave();
    }
}
