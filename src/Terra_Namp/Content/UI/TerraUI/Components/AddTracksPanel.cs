using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Selection;
using Terra_Namp.Core.Audio;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.Services;
using Terra_Namp.Core.UI;
using Terra_Namp.Localization;
using Terra_Namp.Networking;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.Utilities.FileBrowser;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class AddTracksPanel : SmartUIElement
{
    private const int Padding = 10;
    private const int DownloadAreaY = 92; // Right after URL + Download buttons
    private const int EntryHeight = 22;
    private const int MaxVisibleEntries = 3;
    private const float ScrollSpeed = 0.5f;
    private const int ScrollPadding = 40;

    private YoutubeLinkField urlField;
    private ControlButton downloadBtn;
    private ControlButton browseBtn;
    private ControlButton importFolderBtn;
    private ControlButton pasteBtn;
    private ControlButton folderFilterBtn;
    private SearchField searchField;
    private ScrollableSongList songList;

    private string importStatusText = "";
    private Color importStatusColor = Color.White * 0.5f;
    private string searchFilter = "";
    private string folderFilter = "";
    private List<string> availableFolders = new();

    // Sequential import queue — processes one file at a time to avoid freezing
    private readonly Queue<(string SourcePath, string FolderName)> importQueue = new();
    private bool isImporting;
    private int importTotal;
    private int importCompleted;
    private int importSkipped;

    // Dynamic layout tracking
    private int lastVisibleCount = -1;
    private float scrollOffset;

    public event Action<string> OnTrackDeleted;

    public override void OnInitialize()
    {
        int contentWidth = TerraMainPanel.PanelWidth - Padding * 2;
        int y = 16;

        // --- URL Section ---
        urlField = new YoutubeLinkField();
        urlField.Left.Set(Padding, 0);
        urlField.Top.Set(y, 0);
        urlField.Width.Set(contentWidth, 0);
        urlField.Height.Set(32, 0);
        Append(urlField);
        y += 38;

        // Button row: Paste | Download
        int btnGap = 4;
        int pasteBtnWidth = 60;
        int downloadBtnWidth = contentWidth - pasteBtnWidth - btnGap;

        pasteBtn = new ControlButton("Paste");
        pasteBtn.Left.Set(Padding, 0);
        pasteBtn.Top.Set(y, 0);
        pasteBtn.Width.Set(pasteBtnWidth, 0);
        pasteBtn.Height.Set(32, 0);
        pasteBtn.OnLeftClick += (evt, args) => OnPasteClick();
        Append(pasteBtn);

        downloadBtn = new ControlButton("Download");
        downloadBtn.Left.Set(Padding + pasteBtnWidth + btnGap, 0);
        downloadBtn.Top.Set(y, 0);
        downloadBtn.Width.Set(downloadBtnWidth, 0);
        downloadBtn.Height.Set(32, 0);
        downloadBtn.OnLeftClick += (evt, args) => OnDownloadClick();
        Append(downloadBtn);

        // --- Elements below download area — positions set by RepositionElements() ---
        browseBtn = new ControlButton("Browse Local Files...");
        browseBtn.Left.Set(Padding, 0);
        browseBtn.Width.Set(contentWidth, 0);
        browseBtn.Height.Set(32, 0);
        browseBtn.OnLeftClick += (evt, args) => OnBrowseClick();
        Append(browseBtn);

        importFolderBtn = new ControlButton("Import Folder...");
        importFolderBtn.Left.Set(Padding, 0);
        importFolderBtn.Width.Set(contentWidth, 0);
        importFolderBtn.Height.Set(32, 0);
        importFolderBtn.OnLeftClick += (evt, args) => OnImportFolderClick();
        Append(importFolderBtn);

        int folderBtnWidth = 80;
        int gap = 4;

        folderFilterBtn = new ControlButton("All");
        folderFilterBtn.Left.Set(Padding, 0);
        folderFilterBtn.Width.Set(folderBtnWidth, 0);
        folderFilterBtn.Height.Set(26, 0);
        folderFilterBtn.OnLeftClick += (evt, args) => CycleFolderFilter();
        Append(folderFilterBtn);

        searchField = new SearchField();
        searchField.Left.Set(Padding + folderBtnWidth + gap, 0);
        searchField.Width.Set(contentWidth - folderBtnWidth - gap, 0);
        searchField.Height.Set(26, 0);
        searchField.OnTextChanged += text =>
        {
            searchFilter = text;
            RefreshSongList();
        };
        Append(searchField);

        songList = new ScrollableSongList();
        songList.Left.Set(Padding, 0);
        songList.Width.Set(contentWidth, 0);
        songList.OnSongDeleted += HandleSongDeleted;
        songList.OnSetAsBossMusic += uuid => SetSpecialTrack(uuid, isDeathMusic: false);
        songList.OnSetAsDeathMusic += uuid => SetSpecialTrack(uuid, isDeathMusic: true);
        Append(songList);

        // Set initial positions
        RepositionElements(0);
    }

    private void RepositionElements(int downloadLines)
    {
        int y = DownloadAreaY + downloadLines * EntryHeight;

        // "Local Files" label drawn in Draw() at y
        browseBtn.Top.Set(y + 14, 0);
        importFolderBtn.Top.Set(y + 50, 0);
        // Import status drawn in Draw() at y + 86

        int tracksY = y + 100;
        folderFilterBtn.Top.Set(tracksY, 0);
        searchField.Top.Set(tracksY, 0);

        int songListY = tracksY + 30;
        songList.Top.Set(songListY, 0);
        int listHeight = 464 - songListY - 4;
        listHeight = listHeight / 28 * 28;
        songList.Height.Set(Math.Max(listHeight, 28), 0);

        Recalculate();
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        AsyncDownloader.PruneFinishedJobs();
        scrollOffset += ScrollSpeed;

        // Count visible entries: download jobs + import line
        int visibleCount = 0;
        var jobs = AsyncDownloader.GetJobs();
        visibleCount += Math.Min(jobs.Count, MaxVisibleEntries);
        if (isImporting)
            visibleCount++;

        if (visibleCount != lastVisibleCount)
        {
            lastVisibleCount = visibleCount;
            RepositionElements(visibleCount);
        }
    }

    public void RefreshSongList()
    {
        if (songList == null) return;

        var (songs, folders) = SongCacheService.GetSongsAndFolders(folderFilter, searchFilter);
        availableFolders = folders;

        songList.Songs.Clear();
        songList.Songs.AddRange(songs);
    }

    private void HandleSongDeleted(string uuid)
    {
        OnTrackDeleted?.Invoke(uuid);
        RefreshSongList();
    }

    private static void SetSpecialTrack(string uuid, bool isDeathMusic)
    {
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        if (isDeathMusic)
            store.DeathMusicUuid = uuid;
        else
            store.BossMusicUuid = uuid;
        store.ForceSave();
    }

    private void CycleFolderFilter()
    {
        folderFilter = SongCacheService.CycleFolder(folderFilter, availableFolders);
        folderFilterBtn.SetText(string.IsNullOrEmpty(folderFilter) ? "All" : folderFilter);
        RefreshSongList();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        Color accentColor = store.PanelColor;
        float labelScale = 0.6f;

        // "YouTube / URL" label
        spriteBatch.DrawString(font, "YouTube / URL",
            new Vector2(bounds.X + Padding, bounds.Y + 2),
            accentColor * 0.6f, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

        // --- Download progress bars (right after URL section) ---
        int contentWidth = TerraMainPanel.PanelWidth - Padding * 2;
        int statusY = bounds.Y + DownloadAreaY;
        int drawn = 0;

        var jobs = AsyncDownloader.GetJobs();
        foreach (var job in jobs)
        {
            if (drawn >= MaxVisibleEntries) break;
            DrawDownloadJob(spriteBatch, font, job, bounds.X + Padding, statusY, contentWidth, accentColor);
            statusY += EntryHeight;
            drawn++;
        }

        // Import status line
        if (isImporting && !string.IsNullOrEmpty(importStatusText))
        {
            spriteBatch.DrawString(font, importStatusText,
                new Vector2(bounds.X + Padding, statusY + 2),
                importStatusColor, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        // "Local Files" label — positioned dynamically above browse button
        float localLabelY = browseBtn.GetDimensions().Y - 14;
        spriteBatch.DrawString(font, "Local Files",
            new Vector2(bounds.X + Padding, localLabelY),
            accentColor * 0.6f, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

        // "Your Tracks" label — positioned dynamically above folder filter
        float tracksLabelY = folderFilterBtn.GetDimensions().Y - 14;
        spriteBatch.DrawString(font, "Your Tracks",
            new Vector2(bounds.X + Padding, tracksLabelY),
            accentColor * 0.5f, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

        base.Draw(spriteBatch);
    }

    private void DrawDownloadJob(SpriteBatch spriteBatch, DynamicSpriteFont font,
        DownloadJob job, int x, int y, int contentWidth, Color accentColor)
    {
        int barWidth = (int)(contentWidth * 0.7f);
        int gap = 4;
        int infoX = x + barWidth + gap;
        int infoWidth = contentWidth - barWidth - gap;
        int barHeight = 18;

        // --- Left side: progress bar ---
        // Background
        DrawingUtils.DrawRoundedRect(spriteBatch,
            new Rectangle(x, y, barWidth, barHeight),
            Color.Black * 0.3f, 2);

        // Fill
        int fillW = (int)(barWidth * job.Progress);
        if (fillW > 0)
        {
            Color fillColor = job.IsFailed ? Color.Red * 0.4f
                            : job.IsComplete ? accentColor * 0.5f
                            : accentColor * 0.35f;

            DrawingUtils.DrawRoundedRect(spriteBatch,
                new Rectangle(x, y, fillW, barHeight),
                fillColor, 2);
        }

        // Scrolling title inside bar
        string title = job.Title ?? TruncateUrl(job.Url);
        if (!string.IsNullOrEmpty(title))
        {
            float textScale = 0.5f;
            Color textColor = job.IsFailed ? Color.Red : Color.White;
            DrawScrollingText(spriteBatch, font, title,
                new Rectangle(x, y, barWidth, barHeight),
                scrollOffset, textScale, textColor);
        }

        // --- Right side: percentage + ETA ---
        string infoText = FormatInfoText(job);
        Color infoColor = job.IsFailed ? Color.Red : Color.White * 0.9f;
        spriteBatch.DrawString(font, infoText,
            new Vector2(infoX, y + 2),
            infoColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawScrollingText(SpriteBatch spriteBatch, DynamicSpriteFont font,
        string text, Rectangle barRect, float offset, float scale, Color color)
    {
        int innerPad = 4;
        int innerWidth = barRect.Width - innerPad * 2;
        float textWidth = font.MeasureString(text).X * scale;

        if (textWidth <= innerWidth)
        {
            // Text fits — draw directly
            spriteBatch.DrawString(font, text,
                new Vector2(barRect.X + innerPad, barRect.Y + 3),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return;
        }

        // Scrolling with scissor clipping (TextBanner pattern)
        var prevScissor = Main.instance.GraphicsDevice.ScissorRectangle;

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
            SamplerState.LinearClamp, DepthStencilState.None,
            new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None },
            null, Main.UIScaleMatrix);

        float xScale = Main.UIScaleMatrix.M11;
        float yScale = Main.UIScaleMatrix.M22;
        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(
            (int)((barRect.X + innerPad) * xScale),
            (int)(barRect.Y * yScale),
            (int)(innerWidth * xScale),
            (int)(barRect.Height * yScale));

        float totalScrollWidth = textWidth + ScrollPadding;
        float scrollPos = offset % totalScrollWidth;
        float posX = barRect.X + innerPad - scrollPos;

        spriteBatch.DrawString(font, text, new Vector2(posX, barRect.Y + 3),
            color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        spriteBatch.DrawString(font, text, new Vector2(posX + totalScrollWidth, barRect.Y + 3),
            color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        Main.instance.GraphicsDevice.ScissorRectangle = prevScissor;
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
            null, Main.UIScaleMatrix);
    }

    private static string FormatInfoText(DownloadJob job)
    {
        if (job.IsFailed)
            return "Failed";
        if (job.IsComplete)
            return job.IsPlaylist ? $"Done! {job.PlaylistCompleted}/{job.PlaylistTotal}" : "Done!";

        int pct = (int)(job.Progress * 100);
        string info = $"{pct}%";

        if (job.IsPlaylist)
            info = $"{job.PlaylistCompleted}/{job.PlaylistTotal} {info}";

        if (job.Speed != null)
            info += $" {job.Speed}";
        if (job.ETA != null)
            info += $" ~{job.ETA}";

        return info;
    }

    private static string TruncateUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "?";
        return url.Length > 40 ? url[..40] + "..." : url;
    }

    private void OnPasteClick()
    {
        string clipboard = SDL2.SDL.SDL_GetClipboardText();
        if (!string.IsNullOrEmpty(clipboard))
            urlField.CurrentValue = clipboard;
    }

    private void OnDownloadClick()
    {
        string url = urlField.CurrentValue?.Trim();
        if (string.IsNullOrEmpty(url))
            return;

        Action<string> refreshAction = _ =>
        {
            Main.QueueMainThreadAction(() =>
            {
                TerraUILoader.GetUIState<TerraState>()?.MainPanel?.RefreshSongList();
                RefreshSongList();
            });
        };

        if (IsLikelyPlaylist(url))
        {
            AsyncDownloader.StartPlaylistDownload(url, refreshAction, message => { });
        }
        else
        {
            AsyncDownloader.StartDownload(url, refreshAction, message => { });
        }

        urlField.CurrentValue = "";
    }

    private static bool IsLikelyPlaylist(string url)
    {
        // Explicit YouTube playlist page: youtube.com/playlist?list=...
        if (url.Contains("/playlist?", StringComparison.OrdinalIgnoreCase))
            return true;
        // YouTube Music playlist
        if (url.Contains("music.youtube.com/playlist", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private void OnBrowseClick()
    {
        MultiNativeFileDialog multiDialog = new();
        ExtensionFilter[] filters = [new ExtensionFilter("Audio files", "mp3", "flac", "wav", "ogg", "m4a", "wma", "aac")];
        string[] files = multiDialog.OpenFilePanelMulti(filters);

        if (files == null)
            return;

        ImportFiles(files, "Singles");
    }

    private void OnImportFolderClick()
    {
        MultiNativeFileDialog dialog = new();
        ExtensionFilter[] filters = [new ExtensionFilter("Audio files", "mp3", "flac", "wav", "ogg", "m4a", "wma", "aac")];
        string[] selected = dialog.OpenFilePanelMulti(filters);

        if (selected == null || selected.Length == 0)
            return;

        string folder = Path.GetDirectoryName(selected[0]);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            return;

        string[] audioExtensions = ["*.mp3", "*.flac", "*.wav", "*.ogg", "*.m4a", "*.wma", "*.aac"];
        var fileList = new List<string>();
        foreach (string ext in audioExtensions)
            fileList.AddRange(Directory.GetFiles(folder, ext));
        string[] files = fileList.ToArray();
        if (files.Length == 0)
        {
            SetImportStatus("No audio files found in folder", Color.Red);
            return;
        }

        string folderName = Path.GetFileName(folder);
        ImportFiles(files, folderName);
    }

    private void ImportFiles(string[] files, string folderName)
    {
        if (isImporting)
        {
            SetImportStatus("Import already in progress, please wait", Color.Red);
            return;
        }

        foreach (string file in files)
            importQueue.Enqueue((file, folderName));

        importTotal = files.Length;
        importCompleted = 0;
        importSkipped = 0;
        isImporting = true;

        SetImportStatus($"Queued {importTotal} file(s)...", PersistentDataStoreSystem.GetDataStore<TerraDataStore>().SecondaryColor);
        ProcessNextImport();
    }

    private void ProcessNextImport()
    {
        if (importQueue.Count == 0)
        {
            isImporting = false;
            string skipInfo = importSkipped > 0 ? $", {importSkipped} skipped" : "";
            SetImportStatus($"Done! {importCompleted} imported{skipInfo}", PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor);
            return;
        }

        var (sourcePath, folderName) = importQueue.Dequeue();
        int current = importTotal - importQueue.Count;
        string originalFileName = Path.GetFileNameWithoutExtension(sourcePath);

        SetImportStatus($"[{current}/{importTotal}] {originalFileName}...", PersistentDataStoreSystem.GetDataStore<TerraDataStore>().SecondaryColor);

        FileImportService.ImportFileAsync(
            sourcePath,
            Terra_Namp.CachePath,
            originalFileName,
            LocalizationHelper.GetGUIText("AsyncMP3Downloader.AddedByUser"),
            folderName,
            result =>
            {
                if (SongRegistry.Instance?.HasHash(result.HashHex) == true)
                {
                    FileImportService.CleanupImport(Terra_Namp.CachePath, result.Uuid);
                    importSkipped++;
                    ProcessNextImport();
                    return;
                }

                SongRegistry.Instance?.RegisterSong(result.Uuid, result.HashHex);
                importCompleted++;

                TerraUILoader.GetUIState<TerraState>()?.MainPanel?.RefreshSongList();
                RefreshSongList();
                ProcessNextImport();
            },
            error =>
            {
                SetImportStatus($"[{current}/{importTotal}] Failed: {originalFileName}", Color.Red);
                ProcessNextImport();
            });
    }

    private void SetImportStatus(string text, Color color)
    {
        importStatusText = text;
        importStatusColor = color;
    }
}
