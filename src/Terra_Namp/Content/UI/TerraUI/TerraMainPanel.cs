using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.Audio;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Components;
using Terra_Namp.Content.UI.TerraUI.Enums;
using Terra_Namp.Content.UI.NowPlayingUI;
using Terra_Namp.Content.UI.SoundpadUI;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.Services;
using Terra_Namp.Core.UI;
using Terra_Namp.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI;

public class TerraMainPanel : DraggableUIElement
{
    public const int PanelWidth = 340;
    public const int PanelHeight = 520;
    private const int TitleBarHeight = 30;
    private const int Padding = 10;

    public PlaybackController ActiveSong { get; set; }

    private string pendingTrackUuid = null;
    private bool pendingTrackForced = false;
    private bool isTrackSwitching = false;
    private string pendingDisplayUuid = null; // shown in UI immediately during fade-out

    // State saved before server-triggered event music (boss/death)
    private string preEventSongUuid = null;
    private float preEventSongProgress = 0f;
    private bool preEventSongWasPaused = false;

    private TabBar tabBar;
    private UIElement playerTab;
    private SettingsPanel settingsTab;
    private AddTracksPanel addTracksTab;
    private SoundpadPanel soundpadTab;

    private SoundpadPlaybackController soundpadPlayback;
    public SoundpadPlaybackController SoundpadPlayback => soundpadPlayback;
    private bool soundpadControllerInitialized = false;

    private SeekBar seekBar;
    private Visualizer visualizer;
    private VolumeSlider volumeSlider;
    private SearchField searchField;
    private ScrollableSongList songList;
    private PlayPauseButton playPauseButton;
    private IconButton settingsButton;
    private IconButton shieldButton;
    private IconButton stopButton;
    private IconButton slowedReverbBtn;
    private AdminPanel adminTab;
    private bool showingSettings;
    private bool showingAdmin;

    private NowPlayingWidget nowPlayingWidget;

    private string searchFilter = "";
    private string folderFilter = "";
    private List<string> availableFolders = new();
    private ControlButton folderFilterBtn;

    public bool SlowedReverbActive { get; set; }

    public override Rectangle DragBox
    {
        get
        {
            var dims = GetDimensions();
            return new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, TitleBarHeight);
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
        int contentWidth = PanelWidth - Padding * 2;
        int contentTop = TitleBarHeight + TabBar.TabBarHeight;

        // --- Tab bar ---
        tabBar = new TabBar();
        tabBar.AddTab("player", "Player");
        tabBar.AddTab("add", "Add");
        tabBar.AddTab("soundpad", "Soundpad");
        tabBar.Left.Set(0, 0);
        tabBar.Top.Set(TitleBarHeight, 0);
        tabBar.Width.Set(PanelWidth, 0);
        tabBar.Height.Set(TabBar.TabBarHeight, 0);
        tabBar.OnTabChanged += OnTabChanged;
        Append(tabBar);

        // --- Player tab content ---
        playerTab = new UIElement();
        playerTab.Left.Set(0, 0);
        playerTab.Top.Set(contentTop, 0);
        playerTab.Width.Set(PanelWidth, 0);
        playerTab.Height.Set(PanelHeight - contentTop, 0);
        Append(playerTab);

        SetupPlayerTab(contentWidth);

        // --- Settings tab content ---
        settingsTab = new SettingsPanel();
        settingsTab.Left.Set(0, 0);
        settingsTab.Top.Set(contentTop, 0);
        settingsTab.Width.Set(PanelWidth, 0);
        settingsTab.Height.Set(PanelHeight - contentTop, 0);
        settingsTab.Activate();
        settingsTab.Recalculate();

        // --- Add tracks tab content ---
        addTracksTab = new AddTracksPanel();
        addTracksTab.Left.Set(0, 0);
        addTracksTab.Top.Set(contentTop, 0);
        addTracksTab.Width.Set(PanelWidth, 0);
        addTracksTab.Height.Set(PanelHeight - contentTop, 0);
        addTracksTab.OnTrackDeleted += OnSongDeleted;
        addTracksTab.Activate();
        addTracksTab.Recalculate();

        // --- Soundpad tab content ---
        soundpadPlayback = new SoundpadPlaybackController(this);

        soundpadTab = new SoundpadPanel();
        soundpadTab.Left.Set(0, 0);
        soundpadTab.Top.Set(contentTop, 0);
        soundpadTab.Width.Set(PanelWidth, 0);
        soundpadTab.Height.Set(PanelHeight - contentTop, 0);
        soundpadTab.SetPlaybackController(soundpadPlayback);
        soundpadTab.Activate();
        soundpadTab.Recalculate();

        // Share the playback controller with the standalone soundpad popup
        var soundpadState = TerraUILoader.GetUIState<SoundpadState>();
        soundpadState?.SetPlaybackController(soundpadPlayback);

        // --- Admin panel (created but not appended until shield button is clicked) ---
        int contentTop2 = TitleBarHeight + TabBar.TabBarHeight;
        adminTab = new AdminPanel();
        adminTab.Left.Set(0, 0);
        adminTab.Top.Set(contentTop2, 0);
        adminTab.Width.Set(PanelWidth, 0);
        adminTab.Height.Set(PanelHeight - contentTop2, 0);
        adminTab.Activate();
        adminTab.Recalculate();

        // --- Shield button (admin panel toggle, left of settings) ---
        int settingsBtnSize = 24;
        shieldButton = new IconButton("Terra_Namp/Assets/UI/Icons/Shield", iconPadding: 4);
        shieldButton.Left.Set(PanelWidth - settingsBtnSize * 2 - 6, 0);
        shieldButton.Top.Set(3, 0);
        shieldButton.Width.Set(settingsBtnSize, 0);
        shieldButton.Height.Set(settingsBtnSize, 0);
        shieldButton.OnLeftClick += (evt, args) => ToggleAdmin();
        Append(shieldButton);

        // --- Stop button (restore vanilla music, hidden when no song is active) ---
        stopButton = new IconButton("Terra_Namp/Assets/UI/Icons/Refresh", iconPadding: 4);
        stopButton.Left.Set(-9999, 0);
        stopButton.Top.Set(3, 0);
        stopButton.Width.Set(settingsBtnSize, 0);
        stopButton.Height.Set(settingsBtnSize, 0);
        stopButton.OnLeftClick += (evt, args) =>
        {
            if (Main.netMode == NetmodeID.MultiplayerClient
                && !ClientPermissionCache.GetLocalPermissions().CanStop)
                return;
            StopCurrentSong();
        };
        Append(stopButton);

        // --- Settings button (top-right corner of title bar) ---
        settingsButton = new IconButton("Terra_Namp/Assets/UI/Icons/Settings", iconPadding: 4);
        settingsButton.Left.Set(PanelWidth - settingsBtnSize - 3, 0);
        settingsButton.Top.Set(3, 0);
        settingsButton.Width.Set(settingsBtnSize, 0);
        settingsButton.Height.Set(settingsBtnSize, 0);
        settingsButton.OnLeftClick += (evt, args) => ToggleSettings();
        Append(settingsButton);

        RefreshSongList();
    }

    private void SetupPlayerTab(int contentWidth)
    {
        int y = 4;

        // NowPlaying section (child element — auto-hidden when playerTab is removed)
        nowPlayingWidget = new NowPlayingWidget();
        nowPlayingWidget.Left.Set(Padding, 0);
        nowPlayingWidget.Top.Set(y, 0);
        nowPlayingWidget.Width.Set(contentWidth, 0);
        nowPlayingWidget.Height.Set(36, 0);
        playerTab.Append(nowPlayingWidget);
        y += 40;

        // SeekBar
        seekBar = new SeekBar();
        seekBar.Left.Set(Padding, 0);
        seekBar.Top.Set(y, 0);
        seekBar.Width.Set(contentWidth, 0);
        seekBar.Height.Set(24, 0);
        seekBar.OnSeek += progress => ActiveSong?.SeekToProgress(progress);
        playerTab.Append(seekBar);
        y += 28;

        // Visualizer
        visualizer = new Visualizer();
        visualizer.Left.Set(Padding, 0);
        visualizer.Top.Set(y, 0);
        visualizer.Width.Set(contentWidth, 0);
        visualizer.Height.Set(60, 0);
        playerTab.Append(visualizer);
        y += 64;

        // Controls row
        SetupControlButtons(ref y, contentWidth);

        // Folder filter button + Search field
        int folderBtnWidth = 80;
        int gap = 4;

        folderFilterBtn = new ControlButton("All");
        folderFilterBtn.Left.Set(Padding, 0);
        folderFilterBtn.Top.Set(y, 0);
        folderFilterBtn.Width.Set(folderBtnWidth, 0);
        folderFilterBtn.Height.Set(26, 0);
        folderFilterBtn.OnLeftClick += (evt, args) => CycleFolderFilter();
        playerTab.Append(folderFilterBtn);

        int reverbBtnWidth = 30;
        int searchWidth = contentWidth - folderBtnWidth - gap - gap - reverbBtnWidth;

        searchField = new SearchField();
        searchField.Left.Set(Padding + folderBtnWidth + gap, 0);
        searchField.Top.Set(y, 0);
        searchField.Width.Set(searchWidth, 0);
        searchField.Height.Set(26, 0);
        searchField.OnTextChanged += text =>
        {
            searchFilter = text;
            RefreshSongList();
        };
        playerTab.Append(searchField);

        slowedReverbBtn = new IconButton("Terra_Namp/Assets/UI/Icons/Waveform", iconPadding: 5);
        slowedReverbBtn.Left.Set(Padding + folderBtnWidth + gap + searchWidth + gap, 0);
        slowedReverbBtn.Top.Set(y, 0);
        slowedReverbBtn.Width.Set(reverbBtnWidth, 0);
        slowedReverbBtn.Height.Set(26, 0);
        slowedReverbBtn.OnLeftClick += (evt, args) =>
        {
            if (Main.netMode == NetmodeID.MultiplayerClient
                && !ClientPermissionCache.GetLocalPermissions().CanPlay)
                return;

            SlowedReverbActive = !SlowedReverbActive;

            if (ActiveSong != null)
                ActiveSong.SlowedReverbEnabled = SlowedReverbActive;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                PacketBuilder.SlowedReverb((byte)Main.myPlayer, SlowedReverbActive).Send();
        };
        playerTab.Append(slowedReverbBtn);
        y += 30;

        // Song list (height snapped to multiple of item height for clean rendering)
        int totalContentHeight = PanelHeight - (TitleBarHeight + TabBar.TabBarHeight);
        int songListHeight = totalContentHeight - y - 4;
        songListHeight = songListHeight / 28 * 28;
        songList = new ScrollableSongList();
        songList.Left.Set(Padding, 0);
        songList.Top.Set(y, 0);
        songList.Width.Set(contentWidth, 0);
        songList.Height.Set(songListHeight, 0);
        songList.OnSongSelected += OnSongSelected;
        songList.OnSongDeleted += OnSongDeleted;
        songList.OnSetAsBossMusic += uuid => SetSpecialTrack(uuid, isDeathMusic: false);
        songList.OnSetAsDeathMusic += uuid => SetSpecialTrack(uuid, isDeathMusic: true);
        playerTab.Append(songList);
    }

    private void SetupControlButtons(ref int y, int contentWidth)
    {
        int btnH = 32;
        int gap = 4;
        int btnW = 36;

        string[] iconPaths = {
            "Terra_Namp/Assets/UI/Icons/Previous",
            "Terra_Namp/Assets/UI/Icons/Rewind",
            "Terra_Namp/Assets/UI/Icons/Play",  // Will show Play/Pause dynamically
            "Terra_Namp/Assets/UI/Icons/Forward",
            "Terra_Namp/Assets/UI/Icons/Next"
        };
        Action[] actions =
        {
            () =>
            {
                if (ActiveSong != null && !ModContent.GetInstance<TerraTrackUpdaterSystem>().CurrentlyForcingSong)
                {
                    string prev = ActiveSong.GetPreviousSongUuid();
                    StopCurrentSong();
                    BeginPlayingSong(prev);
                }
            },
            () => ActiveSong?.Skip(-10),
            () => ActiveSong?.Toggle(),
            () => ActiveSong?.Skip(10),
            () =>
            {
                if (ActiveSong != null && !ModContent.GetInstance<TerraTrackUpdaterSystem>().CurrentlyForcingSong)
                {
                    string next = ActiveSong.GetNextSongUuid();
                    StopCurrentSong();
                    BeginPlayingSong(next);
                }
            },
        };

        int x = Padding;
        for (int i = 0; i < iconPaths.Length; i++)
        {
            int idx = i;

            // Use PlayPauseButton for play/pause control (index 2)
            if (i == 2)
            {
                playPauseButton = new PlayPauseButton(iconPadding: 8); // Extra padding for Play/Pause
                playPauseButton.Left.Set(x, 0);
                playPauseButton.Top.Set(y, 0);
                playPauseButton.Width.Set(btnW, 0);
                playPauseButton.Height.Set(btnH, 0);
                playPauseButton.OnLeftClick += (evt, args) => actions[idx]();
                playerTab.Append(playPauseButton);
            }
            else
            {
                var btn = new IconButton(iconPaths[i]);
                btn.Left.Set(x, 0);
                btn.Top.Set(y, 0);
                btn.Width.Set(btnW, 0);
                btn.Height.Set(btnH, 0);
                btn.OnLeftClick += (evt, args) => actions[idx]();
                playerTab.Append(btn);
            }

            x += btnW + gap;
        }

        // PlayMode toggle button
        x += gap * 2;
        var modeBtn = new IconToggleButton(iconPadding: 8); // Same padding as Play/Pause
        modeBtn.CurrentMode = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PlayMode;
        modeBtn.Left.Set(x, 0);
        modeBtn.Top.Set(y, 0);
        modeBtn.Width.Set(btnW, 0);
        modeBtn.Height.Set(btnH, 0);
        modeBtn.OnLeftClick += (evt, args) =>
        {
            var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            store.PlayMode++;
            if (store.PlayMode > PlayMode.Loop)
                store.PlayMode = PlayMode.Next;
            modeBtn.CurrentMode = store.PlayMode;
            store.ForceSave();
        };
        playerTab.Append(modeBtn);

        // Volume slider (right side of control row, horizontal)
        x += btnW + gap * 2;
        int volSliderLeft = x;
        int volSliderWidth = PanelWidth - Padding - volSliderLeft;
        volumeSlider = new VolumeSlider();
        volumeSlider.Left.Set(volSliderLeft, 0);
        volumeSlider.Top.Set(y, 0);
        volumeSlider.Width.Set(volSliderWidth, 0);
        volumeSlider.Height.Set(btnH, 0);
        playerTab.Append(volumeSlider);

        y += btnH + 8;
    }

    private void OnTabChanged(int index)
    {
        showingSettings = false;
        showingAdmin = false;

        RemoveChild(playerTab);
        RemoveChild(settingsTab);
        RemoveChild(addTracksTab);
        RemoveChild(soundpadTab);
        RemoveChild(adminTab);

        ShowActiveTab();
        Recalculate();
    }

    private void ToggleSettings()
    {
        showingSettings = !showingSettings;
        showingAdmin = false;

        RemoveChild(playerTab);
        RemoveChild(settingsTab);
        RemoveChild(addTracksTab);
        RemoveChild(soundpadTab);
        RemoveChild(adminTab);

        if (showingSettings)
        {
            Append(settingsTab);
        }
        else
        {
            ShowActiveTab();
        }

        Recalculate();
    }

    private void ToggleAdmin()
    {
        if (!ClientPermissionCache.IsLocalPlayerAdmin())
            return;

        showingAdmin = !showingAdmin;
        showingSettings = false;

        RemoveChild(playerTab);
        RemoveChild(settingsTab);
        RemoveChild(addTracksTab);
        RemoveChild(soundpadTab);
        RemoveChild(adminTab);

        if (showingAdmin)
        {
            Append(adminTab);
        }
        else
        {
            ShowActiveTab();
        }

        Recalculate();
    }

    private void ShowActiveTab()
    {
        switch (tabBar.ActiveTabId)
        {
            case "player":
                Append(playerTab);
                break;
            case "add":
                addTracksTab.RefreshSongList();
                Append(addTracksTab);
                break;
            case "soundpad":
                Append(soundpadTab);
                break;
        }
    }

    public override void DraggableDraw(SpriteBatch spriteBatch)
    {
        // Update play/pause button state
        if (playPauseButton != null)
            playPauseButton.IsPlaying = ActiveSong?.IsPlaying ?? false;

        // Update slowed+reverb button state and ensure new tracks get the effect
        if (slowedReverbBtn != null)
        {
            slowedReverbBtn.IsActive = SlowedReverbActive;
            if (ActiveSong != null && ActiveSong.SlowedReverbEnabled != SlowedReverbActive)
                ActiveSong.SlowedReverbEnabled = SlowedReverbActive;
        }

        // Update settings button state
        if (settingsButton != null)
            settingsButton.IsActive = showingSettings;

        // Shield button visibility: only in multiplayer + admin
        // Stop button visibility: only when a song is loaded (paused or playing)
        if (shieldButton != null)
        {
            bool shouldShowShield = Main.netMode == NetmodeID.MultiplayerClient
                && ClientPermissionCache.IsLocalPlayerAdmin();
            bool canStop = Main.netMode != NetmodeID.MultiplayerClient
                || ClientPermissionCache.GetLocalPermissions().CanStop;
            bool shouldShowStop = ActiveSong != null && canStop;

            int s = 24;
            // Layout from right: [Settings(313)] [Shield(286, optional)] [Stop(optional)]
            // Stop sits to the left of Shield (if visible) or at Shield's slot otherwise
            if (shouldShowShield)
            {
                shieldButton.Left.Set(PanelWidth - s * 2 - 6, 0);
                stopButton?.Left.Set(PanelWidth - s * 3 - 9, 0);
            }
            else
            {
                shieldButton.Left.Set(-9999, 0);
                stopButton?.Left.Set(PanelWidth - s * 2 - 6, 0);

                // Close admin panel if it was showing
                if (showingAdmin)
                {
                    showingAdmin = false;
                    RemoveChild(adminTab);
                    ShowActiveTab();
                    Recalculate();
                }
            }

            shieldButton.IsActive = showingAdmin;
        }

        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        Rectangle drawBox = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        Color accentColor = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;
        Color backgroundColor = store.PanelBackgroundColor;
        float opacity = store.PanelOpacity;
        int cornerRadius = store.CornerRadius;

        // Рисуем размытый фон из глобального BlurRenderTarget SilkyUI
        if (store.BlurLevel > 0)
        {
            BlurHelper.DrawBlurredBackground(spriteBatch, drawBox, store.BlurLevel, cornerRadius);
        }

        // Panel background с прозрачностью
        DrawingUtils.DrawRoundedRect(spriteBatch, drawBox, backgroundColor * opacity, cornerRadius);

        // Border with accent color
        DrawingUtils.DrawRoundedBorder(spriteBatch, drawBox, accentColor * 0.3f, cornerRadius);

        // Title bar background (rounded top corners only)
        Rectangle titleBar = new(drawBox.X + 1, drawBox.Y + 1, drawBox.Width - 2, TitleBarHeight);
        // Vector4: topLeft, topRight, bottomRight, bottomLeft
        DrawingUtils.DrawRoundedRect(spriteBatch, titleBar, Color.Black * 0.3f,
            new Vector4(cornerRadius, cornerRadius, 0, 0));

        // Title text
        float titleScale = 0.8f;
        spriteBatch.DrawString(font, "Terra Namp",
            new Vector2(drawBox.X + 10, drawBox.Y + (TitleBarHeight - font.MeasureString("A").Y * titleScale) / 2f),
            accentColor, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        // Update NowPlaying widget data (it's a child of playerTab, auto-hidden when tab is removed)
        if (nowPlayingWidget != null)
        {
            nowPlayingWidget.SongTitle = ActiveSong?.Name ?? "---";
            nowPlayingWidget.SongAuthor = ActiveSong?.Author ?? "";
        }
    }

    public override void DraggableUpdate(GameTime gameTime)
    {
        // Always update soundpad playback — soundpadTab may be removed from tree when another tab is active
        soundpadPlayback?.Update();

        // Lazy initialization of soundpad controller in popup (in case popup state was created after main panel)
        if (!soundpadControllerInitialized && soundpadPlayback != null)
        {
            var soundpadState = TerraUILoader.GetUIState<SoundpadState>();
            if (soundpadState != null)
            {
                soundpadState.SetPlaybackController(soundpadPlayback);
                soundpadControllerInitialized = true;
            }
        }

        if (ActiveSong != null)
        {
            seekBar.Progress = ActiveSong.Progress;
            seekBar.ElapsedTime = ActiveSong.ElapsedTime;
            seekBar.Duration = ActiveSong.SongDuration;
            visualizer.AudioData = ActiveSong.BufferToSubmit;
            songList.ActiveSongUuid = ActiveSong.Uuid;
        }
        else
        {
            seekBar.Progress = 0;
            seekBar.ElapsedTime = TimeSpan.Zero;
            seekBar.Duration = TimeSpan.Zero;
            visualizer.AudioData = null;
            songList.ActiveSongUuid = null;
        }
    }

    protected override void OnDragEnd(Vector2 position)
    {
        var dims = GetDimensions();
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        store.WindowPositionX = (position.X + dims.Width / 2f) / Main.screenWidth;
        store.WindowPositionY = (position.Y + dims.Height / 2f) / Main.screenHeight;
        store.ForceSave();
    }

    // --- Public API (used by network handlers and TerraTrackUpdaterSystem) ---

    /// <summary>
    /// Play a song locally only — no network sync, no permission check.
    /// Used for boss/death music that is personal to each player.
    /// </summary>
    public void BeginPlayingSongLocalOnly(string uuid)
    {
        NetLogger.Info($"[BeginPlayingSongLocalOnly] uuid={uuid[..8]}..");
        BeginPlayingSongLocal(uuid, forced: false);
    }

    public void BeginPlayingSong(string uuid, bool forced = false)
    {
        // Client-side permission check: don't play if not allowed
        if (Main.netMode == NetmodeID.MultiplayerClient && !forced
            && !ClientPermissionCache.GetLocalPermissions().CanPlay)
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient && !forced)
        {
            string hashHex = SongRegistry.Instance.GetHashByUuid(uuid);
            if (hashHex != null)
            {
                byte[] hash = ContentHash.HexToHash(hashHex);
                string title = "";
                string author = "";

                string titlePath = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");
                if (File.Exists(titlePath))
                {
                    string[] lines = File.ReadAllLines(titlePath);
                    if (lines.Length >= 1) title = lines[0];
                    if (lines.Length >= 2) author = lines[1];
                }

                NetLogger.Info($"BeginPlayingSong: sending PlaySong to server hash={hashHex[..8]}.. title=\"{title}\"");
                PacketBuilder.PlaySong((byte)Main.myPlayer, hash, title, author, false).Send();
            }
        }

        NetLogger.Info($"BeginPlayingSong: uuid={uuid[..8]}.. forced={forced}");
        BeginPlayingSongLocal(uuid, forced);

        // Send prefetch list for upcoming songs in multiplayer.
        if (Main.netMode == NetmodeID.MultiplayerClient && !forced && ActiveSong != null)
            SendPrefetchList();
    }

    private void SendPrefetchList()
    {
        var (allSongs, _) = SongCacheService.GetSongsAndFolders();
        if (allSongs.Count == 0)
            return;

        var songs = new List<(byte[] Hash, string Title, string Author)>();
        foreach (var (title, uuid) in allSongs)
        {
            string hashHex = SongRegistry.Instance.GetHashByUuid(uuid);
            if (hashHex == null) continue;

            string author = "";
            string txtPath = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");
            if (File.Exists(txtPath))
            {
                string[] lines = File.ReadAllLines(txtPath);
                if (lines.Length >= 2) author = lines[1];
            }

            songs.Add((ContentHash.HexToHash(hashHex), title, author));
        }

        if (songs.Count > 0)
        {
            NetLogger.Transfer($"SendPrefetchList: {songs.Count} songs (full library)");
            PacketBuilder.PrefetchList((byte)Main.myPlayer, songs).Send();
        }
    }

    /// <summary>
    /// Called when server broadcasts event music (boss/death). Saves current player state
    /// so it can be restored when the event ends (RestorePreEventState).
    /// </summary>
    public void BeginPlayingEventSong(string uuid, bool forced)
    {
        NetLogger.Info($"BeginPlayingEventSong: uuid={uuid[..8]}.. forced={forced}");

        if (ActiveSong != null)
        {
            preEventSongUuid = ActiveSong.Uuid;
            preEventSongProgress = ActiveSong.Progress;
            preEventSongWasPaused = ActiveSong.IsPaused;
            NetLogger.Info($"BeginPlayingEventSong: saved pre-event state uuid={preEventSongUuid[..8]}.. progress={preEventSongProgress:F3} paused={preEventSongWasPaused}");
        }
        else
        {
            preEventSongUuid = null;
            preEventSongProgress = 0f;
            preEventSongWasPaused = false;
        }

        BeginPlayingSongLocal(uuid, forced);
    }

    /// <summary>
    /// Called when server-triggered event music ends. Restores the player state that was
    /// active before the event, or fully stops if nothing was playing.
    /// </summary>
    public void RestorePreEventState()
    {
        NetLogger.Info($"RestorePreEventState: preEventSongUuid={preEventSongUuid ?? "null"}");

        if (preEventSongUuid != null)
        {
            string restoreUuid = preEventSongUuid;
            float restoreProgress = preEventSongProgress;
            bool restorePaused = preEventSongWasPaused;

            preEventSongUuid = null;

            // Start the track (may fade out boss music first if it's still playing)
            BeginPlayingSongLocal(restoreUuid, forced: false);

            // Seek and pause state applied in UpdateActiveSong once the target track starts.
            // Works for both paths: fade-out (isTrackSwitching) and immediate start (was paused by soundpad).
            pendingRestoreForUuid = restoreUuid;
            pendingRestoreProgress = restoreProgress;
            pendingRestorePaused = restorePaused;
            hasPendingRestore = true;
        }
        else
        {
            // Nothing was playing before → just stop
            preEventSongUuid = null;
            StopCurrentSongLocal();
        }
    }

    private bool hasPendingRestore = false;
    private string pendingRestoreForUuid = null;
    private float pendingRestoreProgress = 0f;
    private bool pendingRestorePaused = false;

    public void BeginPlayingSongFromNetwork(string uuid, bool forced)
    {
        NetLogger.Info($"BeginPlayingSongFromNetwork: uuid={uuid[..8]}.. forced={forced}");
        BeginPlayingSongLocal(uuid, forced);
    }

    public void BeginPlayingSongFromNetwork(string uuid, bool forced, float seekProgress)
    {
        NetLogger.Info($"BeginPlayingSongFromNetwork: uuid={uuid[..8]}.. forced={forced} seek={seekProgress:F3}");
        BeginPlayingSongLocal(uuid, forced);
        ActiveSong?.SeekFromNetwork(seekProgress);
    }

    private void BeginPlayingSongLocal(string uuid, bool forced)
    {
        // If already switching, queue the next track
        if (isTrackSwitching)
        {
            pendingTrackUuid = uuid;
            pendingTrackForced = forced;
            return;
        }

        // If there's an active song, fade it out first
        if (ActiveSong != null && ActiveSong.IsPlaying)
        {
            pendingTrackUuid = uuid;
            pendingTrackForced = forced;
            isTrackSwitching = true;
            ActiveSong.PauseFromNetwork(); // Triggers fade-out
            // Update UI immediately — no reason to wait for the fade to finish
            pendingDisplayUuid = uuid;
            RefreshSongList();
        }
        else
        {
            // No active song or already stopped - start immediately
            ActiveSong?.Dispose();
            ActiveSong = new PlaybackController(uuid, this, forced);

            if (ActiveSong.Failed)
            {
                ActiveSong.Dispose();
                ActiveSong = null;
                RefreshSongList();
                return;
            }

            TerraUILoader.GetUIState<NowPlayingState>().NotifyActiveSong(ActiveSong.Name, ActiveSong.Author);
            RefreshSongList();
        }
    }

    public void StopCurrentSong()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && ActiveSong != null)
        {
            NetLogger.Info("StopCurrentSong: sending StopSong to server");
            PacketBuilder.StopSong((byte)Main.myPlayer).Send();
        }

        NetLogger.Info("StopCurrentSong: stopping locally");
        StopCurrentSongLocal();
    }

    public void StopCurrentSongFromNetwork()
    {
        NetLogger.Info("StopCurrentSongFromNetwork: stopping locally");
        StopCurrentSongLocal();
    }

    private void StopCurrentSongLocal()
    {
        // If track is switching, cancel the pending track
        if (isTrackSwitching)
        {
            isTrackSwitching = false;
            pendingTrackUuid = null;
            pendingDisplayUuid = null;
        }

        // Fade out before stopping
        if (ActiveSong != null && ActiveSong.IsPlaying)
        {
            pendingTrackUuid = null; // Signal that we want to stop, not switch
            isTrackSwitching = true;
            ActiveSong.PauseFromNetwork();
        }
        else
        {
            ActiveSong?.Dispose();
            ActiveSong = null;
            RefreshSongList();
        }
    }

    public void UpdateActiveSong()
    {
        ActiveSong?.UpdateAudioTrack();
        ActiveSong?.SetVolume(volumeSlider.Volume);

        // Handle track switching: when fade-out completes
        if (isTrackSwitching && ActiveSong != null && ActiveSong.IsPaused)
        {
            ActiveSong.Dispose();

            if (pendingTrackUuid != null)
            {
                // Switch to pending track
                ActiveSong = new PlaybackController(pendingTrackUuid, this, pendingTrackForced);
                if (ActiveSong.Failed)
                {
                    ActiveSong.Dispose();
                    ActiveSong = null;
                }
                else
                {
                    TerraUILoader.GetUIState<NowPlayingState>().NotifyActiveSong(ActiveSong.Name, ActiveSong.Author);
                }
            }
            else
            {
                // Stop completely
                ActiveSong = null;
            }

            pendingDisplayUuid = null;
            RefreshSongList();
            isTrackSwitching = false;
            pendingTrackUuid = null;

            // Apply restore after fade-out completes (isTrackSwitching path)
            if (hasPendingRestore && ActiveSong != null && ActiveSong.Uuid == pendingRestoreForUuid)
            {
                hasPendingRestore = false;
                pendingRestoreForUuid = null;
                if (pendingRestorePaused)
                    ActiveSong.SeekAndPauseFromNetwork(pendingRestoreProgress);
                else if (pendingRestoreProgress > 0f)
                    ActiveSong.SeekFromNetwork(pendingRestoreProgress);
            }
        }

        // Apply restore for immediate-start path: boss music was paused by soundpad when
        // RestorePreEventState was called, so BeginPlayingSongLocal started without fade-out.
        if (hasPendingRestore && ActiveSong != null && !isTrackSwitching &&
            ActiveSong.Uuid == pendingRestoreForUuid && ActiveSong.IsPlaying)
        {
            hasPendingRestore = false;
            pendingRestoreForUuid = null;
            if (pendingRestorePaused)
                ActiveSong.SeekAndPauseFromNetwork(pendingRestoreProgress);
            else if (pendingRestoreProgress > 0f)
                ActiveSong.SeekFromNetwork(pendingRestoreProgress);
        }
    }

    public string FolderFilter => folderFilter;

    public void RefreshSongList()
    {
        if (songList == null) return;

        var (songs, folders) = SongCacheService.GetSongsAndFolders(folderFilter, searchFilter);
        availableFolders = folders;

        songList.Songs.Clear();
        songList.Songs.AddRange(songs);
        songList.ActiveSongUuid = pendingDisplayUuid ?? ActiveSong?.Uuid;
    }

    private void CycleFolderFilter()
    {
        folderFilter = SongCacheService.CycleFolder(folderFilter, availableFolders);
        folderFilterBtn.SetText(string.IsNullOrEmpty(folderFilter) ? "All" : folderFilter);
        RefreshSongList();
    }

    // --- Private event handlers ---

    private void OnSongSelected(string uuid)
    {
        if (ActiveSong != null && ActiveSong.Uuid == uuid)
        {
            ActiveSong.Toggle();
            return;
        }

        if (!ModContent.GetInstance<TerraTrackUpdaterSystem>().CurrentlyForcingSong)
        {
            if (ActiveSong != null) StopCurrentSong();
            BeginPlayingSong(uuid);
        }
    }

    private void OnSongDeleted(string uuid)
    {
        if (ActiveSong != null && uuid == ActiveSong.Uuid)
        {
            StopCurrentSong();
            // Force garbage collection to release file handles
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        SongCacheService.DeleteSongFiles(uuid);

        // Refresh both player and add tabs
        RefreshSongList();
        addTracksTab?.RefreshSongList();
    }

    private void SetSpecialTrack(string uuid, bool isDeathMusic)
    {
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        var sStore = PersistentDataStoreSystem.GetDataStore<Content.IO.SoundpadDataStore>();

        if (isDeathMusic)
        {
            store.DeathMusicUuid = uuid;
            sStore.DeathSoundUuid = ""; // mutually exclusive: clear soundpad slot
            NetLogger.Info($"[SetSpecialTrack] DeathMusicUuid set to uuid={uuid[..8]}..");
        }
        else
        {
            store.BossMusicUuid = uuid;
            sStore.BossSoundUuid = ""; // mutually exclusive: clear soundpad slot
            NetLogger.Info($"[SetSpecialTrack] BossMusicUuid set to uuid={uuid[..8]}..");
        }
        store.ForceSave();
        sStore.ForceSave();

        // In multiplayer, notify server so it can trigger playback for all clients on event
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            string hashHex = SongRegistry.Instance.GetHashByUuid(uuid);
            if (hashHex != null)
            {
                byte[] hash = ContentHash.HexToHash(hashHex);
                string title = "", author = "";
                string txtPath = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");
                if (File.Exists(txtPath))
                {
                    string[] lines = File.ReadAllLines(txtPath);
                    if (lines.Length >= 1) title = lines[0];
                    if (lines.Length >= 2) author = lines[1];
                }
                if (isDeathMusic)
                    PacketBuilder.SetDeathTrack((byte)Main.myPlayer, hash, title, author).Send();
                else
                    PacketBuilder.SetBossTrack((byte)Main.myPlayer, hash, title, author).Send();
            }
        }
    }

}
