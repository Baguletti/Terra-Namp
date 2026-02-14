using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Content.UI.TerraUI.Components;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.UI;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terra_Namp.Content.UI.SoundpadUI;

public class SoundpadPopup : DraggableUIElement
{
    public const int PopupWidth = 340;
    public const int PopupHeight = 340;
    private const int TitleBarHeight = 28;
    private const int Padding = 10;
    private const int ButtonsPerRow = 5;
    private const int ButtonSize = 56;
    private const int ButtonGap = 6;
    private const int ItemsPerPage = 15; // 5 cols × 3 rows
    private const int NavButtonSize = 24;
    private const int GridWidth = ButtonsPerRow * ButtonSize + (ButtonsPerRow - 1) * ButtonGap;

    private SoundpadPlaybackController playbackController;
    private HorizontalSlider volumeSlider;
    private int currentPage;

    private Texture2D chevronLeft;
    private Texture2D chevronRight;

    private PressAnimator[] padAnimators;
    private PressAnimator navLeftAnimator;
    private PressAnimator navRightAnimator;

    public override Rectangle DragBox
    {
        get
        {
            var dims = GetDimensions();
            return new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, TitleBarHeight);
        }
    }

    public override Vector2 DefaultPosition => new(0.5f, 0.5f);

    public void SetPlaybackController(SoundpadPlaybackController controller)
    {
        playbackController = controller;
    }

    public override void SafeOnInitialize()
    {
        var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();

        volumeSlider = new HorizontalSlider
        {
            Label = "Soundpad Volume",
            Value = store.VolumeLevel, // Direct linear value (same as main player)
            MinValue = 0f,
            MaxValue = 1f,
            FormatValue = v => $"{(int)(v * 100)}%" // Show linear percentage
        };

        // Position slider 10px from bottom
        volumeSlider.Left.Set(Padding, 0);
        volumeSlider.Top.Set(PopupHeight - 50, 0);
        volumeSlider.Width.Set(PopupWidth - Padding * 2, 0);
        volumeSlider.Height.Set(30, 0);

        volumeSlider.OnValueChanged += sliderVal =>
        {
            // Store linear value directly (cubic curve applied in playback controller)
            store.VolumeLevel = sliderVal;
            store.ForceSave();
        };

        Append(volumeSlider);
        volumeSlider.Activate();
        volumeSlider.Recalculate();

        chevronLeft = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/Chevron_left", AssetRequestMode.ImmediateLoad).Value;
        chevronRight = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/Chevron_right", AssetRequestMode.ImmediateLoad).Value;

        padAnimators = new PressAnimator[ItemsPerPage];
        for (int i = 0; i < ItemsPerPage; i++)
            padAnimators[i] = new PressAnimator();

        navLeftAnimator = new PressAnimator();
        navRightAnimator = new PressAnimator();
    }

    public override void DraggableDraw(SpriteBatch spriteBatch)
    {

        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        var soundStore = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
        Rectangle drawBox = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        Color accent = store.PanelColor;
        float opacity = store.PanelOpacity;

        // Background — synced with main player settings
        int cr = store.CornerRadius;
        if (store.BlurLevel > 0)
            BlurHelper.DrawBlurredBackground(spriteBatch, drawBox, store.BlurLevel, cr);
        DrawingUtils.DrawRoundedRect(spriteBatch, drawBox, Color.Black * opacity, cr);
        DrawingUtils.DrawRoundedBorder(spriteBatch, drawBox, accent * 0.3f, cr);

        // Title bar
        Rectangle titleBar = new(drawBox.X + 1, drawBox.Y + 1, drawBox.Width - 2, TitleBarHeight);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, titleBar, Color.Black * 0.3f);

        float titleScale = 0.7f;
        spriteBatch.DrawString(font, "Soundpad",
            new Vector2(drawBox.X + 10, drawBox.Y + (TitleBarHeight - font.MeasureString("A").Y * titleScale) / 2f),
            accent, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        // Pagination
        int totalItems = soundStore.Sounds.Count;
        int totalPages = Math.Max(1, (totalItems + ItemsPerPage - 1) / ItemsPerPage);
        currentPage = Math.Clamp(currentPage, 0, totalPages - 1);
        int startIndex = currentPage * ItemsPerPage;
        int endIndex = Math.Min(startIndex + ItemsPerPage, totalItems);

        // Sound buttons grid (paginated)
        int y = drawBox.Y + TitleBarHeight + 8;
        int gridStartY = y;

        Point mousePoint = Main.MouseScreen.ToPoint();

        for (int idx = startIndex; idx < endIndex; idx++)
        {
            int localIdx = idx - startIndex;
            int col = localIdx % ButtonsPerRow;
            int row = localIdx / ButtonsPerRow;

            int bx = drawBox.X + (drawBox.Width - GridWidth) / 2 + col * (ButtonSize + ButtonGap);
            int by = y + row * (ButtonSize + ButtonGap);

            Rectangle baseBtnRect = new(bx, by, ButtonSize, ButtonSize);
            Rectangle btnRect = padAnimators[localIdx].GetAnimatedBounds(baseBtnRect);
            bool hover = baseBtnRect.Contains(mousePoint);

            DrawingUtils.DrawRoundedRect(spriteBatch, btnRect,
                hover ? accent * 0.15f : Color.White * 0.06f, 5);
            DrawingUtils.DrawRoundedBorder(spriteBatch, btnRect,
                hover ? accent * 0.5f : Color.White * 0.1f, 5);

            string name = soundStore.Sounds[idx].DisplayName;
            float nameScale = 0.5f;
            float nameWidth = EmojiRenderer.MeasureWidth(font, name, nameScale);

            // Truncate if too wide (codepoint-aware to not break surrogate pairs)
            bool truncated = false;
            while (nameWidth > ButtonSize - 8 && name.Length > 0)
            {
                int removeCount = (name.Length >= 2 && char.IsLowSurrogate(name[^1]) && char.IsHighSurrogate(name[^2])) ? 2 : 1;
                name = name[..^removeCount];
                truncated = true;
                nameWidth = EmojiRenderer.MeasureWidth(font, name + "..", nameScale);
            }
            if (truncated)
                name += "..";

            float finalWidth = EmojiRenderer.MeasureWidth(font, name, nameScale);
            float textHeight = font.MeasureString("A").Y * nameScale;
            Vector2 namePos = new(
                btnRect.X + (btnRect.Width - finalWidth) / 2f,
                btnRect.Y + (btnRect.Height - textHeight) / 2f);
            EmojiRenderer.DrawString(spriteBatch, font, name, namePos,
                hover ? accent : Color.White * 0.8f, nameScale);

            if (hover)
                Main.instance.MouseText(soundStore.Sounds[idx].DisplayName);
        }

        if (soundStore.Sounds.Count == 0)
        {
            string empty = "No sounds. Add via main panel.";
            float emptyScale = 0.55f;
            Vector2 emptySize = font.MeasureString(empty) * emptyScale;
            spriteBatch.DrawString(font, empty,
                new Vector2(drawBox.X + (drawBox.Width - emptySize.X) / 2f, y + 40),
                Color.White * 0.3f, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
        }

        // Pagination navigation (only if more than 1 page)
        if (totalPages > 1)
        {
            int maxRows = (ItemsPerPage + ButtonsPerRow - 1) / ButtonsPerRow;
            int navY = gridStartY + maxRows * (ButtonSize + ButtonGap) + 2;
            int navCenterX = drawBox.X + drawBox.Width / 2;

            // Left chevron
            Rectangle baseLeftBtn = new(navCenterX - 40 - NavButtonSize, navY, NavButtonSize, NavButtonSize);
            bool canGoLeft = currentPage > 0;
            Rectangle leftBtn = navLeftAnimator.GetAnimatedBounds(baseLeftBtn);
            bool leftHover = canGoLeft && baseLeftBtn.Contains(mousePoint);
            DrawingUtils.DrawRoundedRect(spriteBatch, leftBtn,
                leftHover ? accent * 0.15f : Color.White * 0.06f, 4);
            if (canGoLeft)
                DrawingUtils.DrawRoundedBorder(spriteBatch, leftBtn,
                    leftHover ? accent * 0.4f : Color.White * 0.1f, 4);
            Rectangle leftIconRect = new(leftBtn.X + 4, leftBtn.Y + 4, leftBtn.Width - 8, leftBtn.Height - 8);
            spriteBatch.Draw(chevronLeft, leftIconRect,
                leftHover ? accent : (canGoLeft ? Color.White * 0.6f : Color.White * 0.2f));

            // Page number
            string pageText = $"{currentPage + 1}";
            float pageScale = 0.6f;
            Vector2 pageSize = font.MeasureString(pageText) * pageScale;
            spriteBatch.DrawString(font, pageText,
                new Vector2(navCenterX - pageSize.X / 2f, navY + (NavButtonSize - pageSize.Y) / 2f),
                accent * 0.8f, 0f, Vector2.Zero, pageScale, SpriteEffects.None, 0f);

            // Right chevron
            Rectangle baseRightBtn = new(navCenterX + 40, navY, NavButtonSize, NavButtonSize);
            bool canGoRight = currentPage < totalPages - 1;
            Rectangle rightBtn = navRightAnimator.GetAnimatedBounds(baseRightBtn);
            bool rightHover = canGoRight && baseRightBtn.Contains(mousePoint);
            DrawingUtils.DrawRoundedRect(spriteBatch, rightBtn,
                rightHover ? accent * 0.15f : Color.White * 0.06f, 4);
            if (canGoRight)
                DrawingUtils.DrawRoundedBorder(spriteBatch, rightBtn,
                    rightHover ? accent * 0.4f : Color.White * 0.1f, 4);
            Rectangle rightIconRect = new(rightBtn.X + 4, rightBtn.Y + 4, rightBtn.Width - 8, rightBtn.Height - 8);
            spriteBatch.Draw(chevronRight, rightIconRect,
                rightHover ? accent : (canGoRight ? Color.White * 0.6f : Color.White * 0.2f));
        }

        if (IsMouseHovering)
            Main.LocalPlayer.mouseInterface = true;

        base.DraggableDraw(spriteBatch);
    }

    public override void DraggableUpdate(GameTime gameTime)
    {
        // Block all interaction when menu/inventory is open
        if (Main.gameMenu || Main.playerInventory)
        {
            playbackController?.Update();
            return;
        }

        // Sync volume slider with store (unless user is dragging it)
        if (volumeSlider != null && !volumeSlider.IsDragging)
        {
            var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
            if (Math.Abs(volumeSlider.Value - store.VolumeLevel) > 0.001f)
            {
                volumeSlider.Value = store.VolumeLevel;
            }
        }

        // Update pad press animations
        if (padAnimators != null)
        {
            Rectangle drawBox = GetDimensions().ToRectangle();
            bool mouseDown = Main.mouseLeft;
            Point mouse = Main.MouseScreen.ToPoint();

            int gridY = drawBox.Y + TitleBarHeight + 8;

            for (int i = 0; i < ItemsPerPage; i++)
            {
                bool isPressed = false;
                if (mouseDown)
                {
                    int col = i % ButtonsPerRow;
                    int row = i / ButtonsPerRow;
                    int bx = drawBox.X + (drawBox.Width - GridWidth) / 2 + col * (ButtonSize + ButtonGap);
                    int by = gridY + row * (ButtonSize + ButtonGap);
                    Rectangle btnRect = new(bx, by, ButtonSize, ButtonSize);
                    isPressed = btnRect.Contains(mouse);
                }
                padAnimators[i].Update(isPressed);
            }
        }

        // Update nav button animations
        {
            Rectangle drawBox2 = GetDimensions().ToRectangle();
            var soundStore = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
            int totalItems = soundStore.Sounds.Count;
            int totalPages = Math.Max(1, (totalItems + ItemsPerPage - 1) / ItemsPerPage);
            bool md = Main.mouseLeft;
            Point mp = Main.MouseScreen.ToPoint();

            if (totalPages > 1)
            {
                int maxRows = (ItemsPerPage + ButtonsPerRow - 1) / ButtonsPerRow;
                int navY = drawBox2.Y + TitleBarHeight + 8 + maxRows * (ButtonSize + ButtonGap) + 2;
                int navCenterX = drawBox2.X + drawBox2.Width / 2;

                Rectangle leftBtn = new(navCenterX - 40 - NavButtonSize, navY, NavButtonSize, NavButtonSize);
                Rectangle rightBtn = new(navCenterX + 40, navY, NavButtonSize, NavButtonSize);

                navLeftAnimator.Update(md && leftBtn.Contains(mp) && currentPage > 0);
                navRightAnimator.Update(md && rightBtn.Contains(mp) && currentPage < totalPages - 1);
            }
            else
            {
                navLeftAnimator.Update(false);
                navRightAnimator.Update(false);
            }
        }

        playbackController?.Update();
    }

    public override void SafeClick(UIMouseEvent evt)
    {
        // Block clicks when any menu/inventory is open to prevent click-through
        if (Main.gameMenu || Main.playerInventory)
            return;

        var soundStore = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
        Rectangle drawBox = GetDimensions().ToRectangle();
        Point mouse = Main.MouseScreen.ToPoint();

        int y = drawBox.Y + TitleBarHeight + 8;

        // Pagination
        int totalItems = soundStore.Sounds.Count;
        int totalPages = Math.Max(1, (totalItems + ItemsPerPage - 1) / ItemsPerPage);
        int startIndex = currentPage * ItemsPerPage;
        int endIndex = Math.Min(startIndex + ItemsPerPage, totalItems);

        for (int idx = startIndex; idx < endIndex; idx++)
        {
            int localIdx = idx - startIndex;
            int col = localIdx % ButtonsPerRow;
            int row = localIdx / ButtonsPerRow;

            int bx = drawBox.X + (drawBox.Width - GridWidth) / 2 + col * (ButtonSize + ButtonGap);
            int by = y + row * (ButtonSize + ButtonGap);

            Rectangle btnRect = new(bx, by, ButtonSize, ButtonSize);
            if (btnRect.Contains(mouse))
            {
                playbackController?.PlaySound(soundStore.Sounds[idx].Uuid);
                return;
            }
        }

        // Check pagination buttons
        if (totalPages > 1)
        {
            int maxRows = (ItemsPerPage + ButtonsPerRow - 1) / ButtonsPerRow;
            int navY = y + maxRows * (ButtonSize + ButtonGap) + 2;
            int navCenterX = drawBox.X + drawBox.Width / 2;

            Rectangle leftBtn = new(navCenterX - 40 - NavButtonSize, navY, NavButtonSize, NavButtonSize);
            if (leftBtn.Contains(mouse) && currentPage > 0)
            {
                currentPage--;
                return;
            }

            Rectangle rightBtn = new(navCenterX + 40, navY, NavButtonSize, NavButtonSize);
            if (rightBtn.Contains(mouse) && currentPage < totalPages - 1)
            {
                currentPage++;
                return;
            }
        }
    }
}
