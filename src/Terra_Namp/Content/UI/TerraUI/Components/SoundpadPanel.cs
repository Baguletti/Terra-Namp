using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.Services;
using Terra_Namp.Core.UI;
using Terra_Namp.Networking;
using ReLogic.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.Utilities.FileBrowser;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class SoundpadPanel : SmartUIElement
{
    private const int Padding = 10;
    private const int ButtonsPerRow = 5;
    private const int ButtonSize = 56;
    private const int ButtonGap = 6;
    private const int NameFieldHeight = 26;
    private const int ItemsPerPage = 25; // 5 cols × 5 rows
    private const int NavButtonSize = 24;
    private const int GridWidth = ButtonsPerRow * ButtonSize + (ButtonsPerRow - 1) * ButtonGap;
    private const int EmojiBtnSize = 26;

    // Emoji picker
    private const int EmojiPickerCellSize = 32;
    private const int EmojiPickerHeight = 200; // 6 rows × 32 + 4+4 padding
    private const int EmojiScrollbarHitWidth = 10;

    private SoundpadPlaybackController playbackController;
    private readonly TextInputHandler nameInput = new();
    private bool nameFieldFocused;
    private string statusMessage;
    private int statusTimer;
    private HorizontalSlider volumeSlider;
    private int currentPage;

    private Texture2D chevronLeft;
    private Texture2D chevronRight;

    // Emoji picker state
    private bool showEmojiPicker;
    private float emojiScrollOffset;
    private int[] emojiCodepoints;
    private Rectangle emojiPickerBounds;
    private int emojiPickerCols;
    private bool emojiScrollDragging;
    private float emojiScrollDragOffsetY;

    private readonly ContextMenu contextMenu = new();

    // Press animations
    private PressAnimator[] padAnimators;
    private PressAnimator addBtnAnimator;
    private PressAnimator navLeftAnimator;
    private PressAnimator navRightAnimator;

    public void SetPlaybackController(SoundpadPlaybackController controller)
    {
        playbackController = controller;
    }

    public override void OnInitialize()
    {
        base.OnInitialize();

        const int PanelWidth = 340;
        const int PanelHeight = 520;
        const int TitleBarHeight = 30;
        const int TabBarHeight = 26;

        int contentWidth = PanelWidth - Padding * 2;
        int contentTop = TitleBarHeight + TabBarHeight;
        int availableHeight = PanelHeight - contentTop;

        int sliderY = availableHeight - 50;

        var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();

        volumeSlider = new HorizontalSlider
        {
            Label = "Soundpad Volume",
            Value = store.VolumeLevel,
            MinValue = 0f,
            MaxValue = 1f,
            FormatValue = v => $"{(int)(v * 100)}%"
        };

        volumeSlider.Left.Set(Padding, 0);
        volumeSlider.Top.Set(sliderY, 0);
        volumeSlider.Width.Set(contentWidth, 0);
        volumeSlider.Height.Set(30, 0);

        volumeSlider.OnValueChanged += sliderVal =>
        {
            store.VolumeLevel = sliderVal;
            store.ForceSave();
        };

        Append(volumeSlider);
        volumeSlider.Activate();
        volumeSlider.Recalculate();

        chevronLeft = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/Chevron_left", AssetRequestMode.ImmediateLoad).Value;
        chevronRight = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Icons/Chevron_right", AssetRequestMode.ImmediateLoad).Value;

        emojiCodepoints = EmojiRenderer.GetSortedCodepoints();

        padAnimators = new PressAnimator[ItemsPerPage];
        for (int i = 0; i < ItemsPerPage; i++)
            padAnimators[i] = new PressAnimator();

        addBtnAnimator = new PressAnimator();
        navLeftAnimator = new PressAnimator();
        navRightAnimator = new PressAnimator();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        var font = FontAssets.MouseText.Value;
        Color accent = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;
        var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();

        int y = bounds.Y + 8;

        // Sound name label
        float labelScale = 0.6f;
        spriteBatch.DrawString(font, "Right-click pad to delete",
            new Vector2(bounds.X + Padding, y + 2),
            Color.White * 0.35f, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        y += 18;

        // Emoji picker button (left of name field, no press animation)
        Rectangle emojiBtnRect = new(bounds.X + Padding, y, EmojiBtnSize, NameFieldHeight);
        bool emojiHover = emojiBtnRect.Contains(Main.MouseScreen.ToPoint());
        DrawingUtils.DrawRoundedRect(spriteBatch, emojiBtnRect,
            (showEmojiPicker || emojiHover) ? accent * 0.2f : Color.White * 0.08f, 4);
        DrawingUtils.DrawRoundedBorder(spriteBatch, emojiBtnRect,
            (showEmojiPicker || emojiHover) ? accent * 0.4f : Color.White * 0.15f, 4);

        if (EmojiRenderer.IsLoaded)
        {
            var glyphMap = EmojiRenderer.GlyphMap;
            if (glyphMap != null && glyphMap.TryGetValue(0x1F600, out Rectangle srcRect))
            {
                int iconSize = EmojiBtnSize - 6;
                Rectangle iconDest = new(
                    emojiBtnRect.X + (EmojiBtnSize - iconSize) / 2,
                    emojiBtnRect.Y + (NameFieldHeight - iconSize) / 2,
                    iconSize, iconSize);
                spriteBatch.Draw(EmojiRenderer.Atlas, iconDest, srcRect, Color.White);
            }
        }

        // Name input field
        int nameX = emojiBtnRect.X + EmojiBtnSize + 4;
        int addBtnWidth = 60;
        int nameFieldWidth = bounds.X + bounds.Width - Padding - addBtnWidth - 4 - nameX;
        Rectangle nameRect = new(nameX, y, nameFieldWidth, NameFieldHeight);
        DrawingUtils.DrawRoundedRect(spriteBatch, nameRect, Color.Black * 0.3f, 4);
        DrawingUtils.DrawRoundedBorder(spriteBatch, nameRect,
            nameFieldFocused ? accent * 0.5f : Color.White * 0.1f, 4);

        // Handle text input
        if (nameFieldFocused)
        {
            nameInput.HandleInput();

            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                nameFieldFocused = false;
                nameInput.Clear();
            }
        }

        string pendingName = nameInput.Text;
        string displayText = string.IsNullOrEmpty(pendingName) && !nameFieldFocused ? "Enter name..." : pendingName;
        Color textColor = string.IsNullOrEmpty(pendingName) && !nameFieldFocused ? Color.White * 0.3f : Color.White * 0.9f;
        float textScale = 0.6f;
        Vector2 textPos = new(nameRect.X + 6, nameRect.Y + (nameRect.Height - font.MeasureString("A").Y * textScale) / 2f);
        EmojiRenderer.DrawString(spriteBatch, font, displayText, textPos, textColor, textScale);

        // Cursor
        if (nameFieldFocused)
            nameInput.DrawCursor(spriteBatch, font, textScale, nameRect.X + 6, nameRect.Y, nameRect.Height, Color.White * 0.8f);

        // Add button
        Rectangle baseAddRect = new(nameRect.X + nameRect.Width + 4, y, addBtnWidth, NameFieldHeight);
        Rectangle addBtnRect = addBtnAnimator.GetAnimatedBounds(baseAddRect);
        bool addHover = baseAddRect.Contains(Main.MouseScreen.ToPoint());
        DrawingUtils.DrawRoundedRect(spriteBatch, addBtnRect, addHover ? accent * 0.2f : Color.White * 0.08f, 3);
        DrawingUtils.DrawRoundedBorder(spriteBatch, addBtnRect, addHover ? accent * 0.4f : Color.White * 0.15f, 3);
        string addLabel = "+ Add";
        Vector2 addSize = font.MeasureString(addLabel) * 0.55f;
        spriteBatch.DrawString(font, addLabel,
            new Vector2(addBtnRect.X + (addBtnRect.Width - addSize.X) / 2f, addBtnRect.Y + (addBtnRect.Height - addSize.Y) / 2f),
            addHover ? accent : Color.White * 0.7f, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        y += NameFieldHeight + 10;

        // Status message (errors/validation only)
        if (statusTimer > 0)
        {
            spriteBatch.DrawString(font, statusMessage,
                new Vector2(bounds.X + Padding, y),
                accent * 0.8f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            y += 16;
        }

        // Pagination
        int totalItems = store.Sounds.Count;
        int totalPages = Math.Max(1, (totalItems + ItemsPerPage - 1) / ItemsPerPage);
        currentPage = Math.Clamp(currentPage, 0, totalPages - 1);
        int startIndex = currentPage * ItemsPerPage;
        int endIndex = Math.Min(startIndex + ItemsPerPage, totalItems);

        // Sound buttons grid (paginated, 5 columns with press animation)
        int gridStartY = y;
        Point mousePoint = Main.MouseScreen.ToPoint();

        for (int idx = startIndex; idx < endIndex; idx++)
        {
            int localIdx = idx - startIndex;
            int col = localIdx % ButtonsPerRow;
            int row = localIdx / ButtonsPerRow;

            int bx = bounds.X + (bounds.Width - GridWidth) / 2 + col * (ButtonSize + ButtonGap);
            int by = gridStartY + row * (ButtonSize + ButtonGap);

            Rectangle baseBtnRect = new(bx, by, ButtonSize, ButtonSize);

            Rectangle btnRect = padAnimators[localIdx].GetAnimatedBounds(baseBtnRect);
            bool hover = baseBtnRect.Contains(mousePoint) && !showEmojiPicker;

            bool isBossPad  = !string.IsNullOrEmpty(store.BossSoundUuid)  && store.BossSoundUuid  == store.Sounds[idx].Uuid;
            bool isDeathPad = !string.IsNullOrEmpty(store.DeathSoundUuid) && store.DeathSoundUuid == store.Sounds[idx].Uuid;
            bool isEventPad = isBossPad || isDeathPad;

            DrawingUtils.DrawRoundedRect(spriteBatch, btnRect,
                hover ? accent * 0.15f : Color.White * 0.06f, 5);
            DrawingUtils.DrawRoundedBorder(spriteBatch, btnRect,
                isEventPad ? accent * 0.9f :
                hover ? accent * 0.5f : Color.White * 0.1f, 5);

            string name = store.Sounds[idx].DisplayName;
            float nameScale = 0.5f;
            float nameW = EmojiRenderer.MeasureWidth(font, name, nameScale);

            bool truncated = false;
            while (nameW > btnRect.Width - 8 && name.Length > 0)
            {
                int removeCount = (name.Length >= 2 && char.IsLowSurrogate(name[^1]) && char.IsHighSurrogate(name[^2])) ? 2 : 1;
                name = name[..^removeCount];
                truncated = true;
                nameW = EmojiRenderer.MeasureWidth(font, name + "..", nameScale);
            }
            if (truncated)
                name += "..";

            float finalWidth = EmojiRenderer.MeasureWidth(font, name, nameScale);
            float textHeight = font.MeasureString("A").Y * nameScale;
            Vector2 namePos = new(
                btnRect.X + (btnRect.Width - finalWidth) / 2f,
                btnRect.Y + (btnRect.Height - textHeight) / 2f);

            Color padTextColor = hover ? accent : Color.White * 0.8f;
            bool behindPicker = showEmojiPicker && row < 3;

            if (behindPicker)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    for (int dy = -4; dy <= 4; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        float dist2 = dx * dx + dy * dy;
                        float weight = 0.055f * MathF.Exp(-dist2 / 8f);
                        if (weight < 0.004f) continue;
                        EmojiRenderer.DrawString(spriteBatch, font, name,
                            namePos + new Vector2(dx, dy), padTextColor * weight, nameScale);
                    }
                }
            }
            else
            {
                EmojiRenderer.DrawString(spriteBatch, font, name, namePos, padTextColor, nameScale);
            }

        }

        // Empty state
        if (store.Sounds.Count == 0)
        {
            string empty = "No sounds yet. Add one!";
            float emptyScale = 0.55f;
            Vector2 emptySize = font.MeasureString(empty) * emptyScale;
            spriteBatch.DrawString(font, empty,
                new Vector2(bounds.X + (bounds.Width - emptySize.X) / 2f, gridStartY + 30),
                Color.White * 0.3f, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
        }

        // Pagination navigation
        if (totalPages > 1)
        {
            int maxRows = (ItemsPerPage + ButtonsPerRow - 1) / ButtonsPerRow;
            int navY = gridStartY + maxRows * (ButtonSize + ButtonGap) + 2;
            int navCenterX = bounds.X + bounds.Width / 2;

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

            string pageText = $"{currentPage + 1}";
            float pageScale = 0.6f;
            Vector2 pageSize = font.MeasureString(pageText) * pageScale;
            spriteBatch.DrawString(font, pageText,
                new Vector2(navCenterX - pageSize.X / 2f, navY + (NavButtonSize - pageSize.Y) / 2f),
                accent * 0.8f, 0f, Vector2.Zero, pageScale, SpriteEffects.None, 0f);

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

        // Emoji picker popup (drawn last = overlay)
        if (showEmojiPicker && emojiCodepoints != null && emojiCodepoints.Length > 0)
        {
            DrawEmojiPicker(spriteBatch, font, accent, bounds);
        }

        if (showEmojiPicker && emojiPickerBounds.Width > 0 && emojiPickerBounds.Contains(mousePoint))
            Main.LocalPlayer.mouseInterface = true;

        // Context menu overlay (drawn after everything else)
        contextMenu.Draw(spriteBatch, accent);
        if (contextMenu.ContainsMouse())
            Main.LocalPlayer.mouseInterface = true;

        base.Draw(spriteBatch);
    }

    private const int VisibleEmojiRows = 6;

    private void DrawEmojiPicker(SpriteBatch spriteBatch, DynamicSpriteFont font, Color accent, Rectangle panelBounds)
    {
        // Full-width picker anchored below the emoji button row
        int pickerWidth = panelBounds.Width - 2 * Padding;
        int pickerX = panelBounds.X + Padding;
        int pickerY = panelBounds.Y + 8 + 18 + NameFieldHeight + 4;
        emojiPickerBounds = new Rectangle(pickerX, pickerY, pickerWidth, EmojiPickerHeight);
        emojiPickerCols = (pickerWidth - 8 - EmojiScrollbarHitWidth) / EmojiPickerCellSize;

        // Background
        var uiStore = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        int cr = uiStore.CornerRadius;
        BlurHelper.DrawBlurredBackground(spriteBatch, emojiPickerBounds, 10, cr);
        DrawingUtils.DrawRoundedRect(spriteBatch, emojiPickerBounds, Color.Black * uiStore.PanelOpacity, cr);

        // Content area (inset by 4px each side)
        int visibleHeight = VisibleEmojiRows * EmojiPickerCellSize;
        Rectangle clipRect = new(pickerX + 4, pickerY + 4, pickerWidth - 8, visibleHeight);

        // Clamp scroll
        int totalRows = (emojiCodepoints.Length + emojiPickerCols - 1) / emojiPickerCols;
        int maxScrollRows = Math.Max(0, totalRows - VisibleEmojiRows);
        emojiScrollOffset = MathHelper.Clamp(emojiScrollOffset, 0, maxScrollRows * EmojiPickerCellSize);

        // Scissor clipping
        var device = spriteBatch.GraphicsDevice;
        var oldScissor = device.ScissorRectangle;

        Rectangle scaledClip = new(
            (int)(clipRect.X * Main.UIScale),
            (int)(clipRect.Y * Main.UIScale),
            (int)(clipRect.Width * Main.UIScale),
            (int)(clipRect.Height * Main.UIScale));

        spriteBatch.End();
        device.ScissorRectangle = scaledClip;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

        // Draw emoji grid
        Point mousePoint = Main.MouseScreen.ToPoint();
        var glyphMap = EmojiRenderer.GlyphMap;
        var atlasTexture = EmojiRenderer.Atlas;

        int firstRow = (int)emojiScrollOffset / EmojiPickerCellSize;

        for (int visRow = 0; visRow < VisibleEmojiRows; visRow++)
        {
            int dataRow = firstRow + visRow;
            if (dataRow >= totalRows) break;

            for (int col = 0; col < emojiPickerCols; col++)
            {
                int idx = dataRow * emojiPickerCols + col;
                if (idx >= emojiCodepoints.Length) break;

                int ex = clipRect.X + col * EmojiPickerCellSize;
                int ey = clipRect.Y + visRow * EmojiPickerCellSize;

                Rectangle cellRect = new(ex, ey, EmojiPickerCellSize, EmojiPickerCellSize);
                bool hover = cellRect.Contains(mousePoint) && !emojiScrollDragging;

                if (hover)
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value, cellRect, accent * 0.2f);

                if (glyphMap != null && glyphMap.TryGetValue(emojiCodepoints[idx], out Rectangle src))
                {
                    int emojiSize = EmojiPickerCellSize * 3 / 4;
                    int pad = (EmojiPickerCellSize - emojiSize) / 2;
                    Rectangle dest = new(ex + pad, ey + pad, emojiSize, emojiSize);
                    spriteBatch.Draw(atlasTexture, dest, src, Color.White);
                }
            }
        }

        // Draggable scrollbar
        float maxScroll = maxScrollRows * EmojiPickerCellSize;
        if (maxScroll > 0)
        {
            float totalContentHeight = totalRows * EmojiPickerCellSize;
            float barHeight = Math.Max(20, (float)visibleHeight * visibleHeight / totalContentHeight);
            float scrollRatio = emojiScrollOffset / maxScroll;
            float barY = clipRect.Y + scrollRatio * (visibleHeight - barHeight);

            Rectangle scrollHitRect = new(clipRect.X + clipRect.Width - EmojiScrollbarHitWidth,
                clipRect.Y, EmojiScrollbarHitWidth, visibleHeight);
            bool scrollHover = scrollHitRect.Contains(mousePoint) || emojiScrollDragging;

            int barW = scrollHover ? 5 : 3;
            Color barColor = scrollHover ? accent * 0.6f : accent * 0.4f;
            Rectangle scrollBar = new(clipRect.X + clipRect.Width - barW - 1, (int)barY, barW, (int)barHeight);
            DrawingUtils.DrawRoundedRect(spriteBatch, scrollBar, barColor, barW / 2);
        }

        // Restore SpriteBatch state
        spriteBatch.End();
        device.ScissorRectangle = oldScissor;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

        DrawingUtils.DrawRoundedBorder(spriteBatch, emojiPickerBounds, accent * 0.3f, cr);
    }

    public override void SafeScrollWheel(UIScrollWheelEvent evt)
    {
        if (showEmojiPicker && emojiCodepoints != null && emojiPickerCols > 0)
        {
            if (evt.ScrollWheelValue > 0)
                emojiScrollOffset -= EmojiPickerCellSize;
            else if (evt.ScrollWheelValue < 0)
                emojiScrollOffset += EmojiPickerCellSize;

            int totalRows = (emojiCodepoints.Length + emojiPickerCols - 1) / emojiPickerCols;
            int maxScrollRows = Math.Max(0, totalRows - VisibleEmojiRows);
            emojiScrollOffset = MathHelper.Clamp(emojiScrollOffset, 0, maxScrollRows * EmojiPickerCellSize);
        }
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        if (statusTimer > 0)
            statusTimer--;

        // Emoji picker scrollbar drag
        if (emojiScrollDragging)
        {
            if (!Main.mouseLeft)
            {
                emojiScrollDragging = false;
            }
            else if (emojiPickerCols > 0)
            {
                int visibleHeight = VisibleEmojiRows * EmojiPickerCellSize;
                int totalRows = (emojiCodepoints.Length + emojiPickerCols - 1) / emojiPickerCols;
                int maxScrollRows = Math.Max(0, totalRows - VisibleEmojiRows);
                float maxScroll = maxScrollRows * EmojiPickerCellSize;
                if (maxScroll > 0)
                {
                    float totalContentHeight = totalRows * EmojiPickerCellSize;
                    float barHeight = Math.Max(20, (float)visibleHeight * visibleHeight / totalContentHeight);
                    float trackHeight = visibleHeight - barHeight;
                    if (trackHeight > 0)
                    {
                        float clipY = emojiPickerBounds.Y + 4;
                        float mouseRelY = Main.MouseScreen.Y - emojiScrollDragOffsetY - clipY;
                        float ratio = MathHelper.Clamp(mouseRelY / trackHeight, 0f, 1f);
                        emojiScrollOffset = ratio * maxScroll;
                    }
                }
            }
        }

        // Sync volume slider with store
        if (volumeSlider != null && !volumeSlider.IsDragging)
        {
            var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
            if (Math.Abs(volumeSlider.Value - store.VolumeLevel) > 0.001f)
            {
                volumeSlider.Value = store.VolumeLevel;
            }
        }

        // Update all press animations
        Rectangle bounds = GetDimensions().ToRectangle();
        bool mouseDown = Main.mouseLeft;
        Point mouse = Main.MouseScreen.ToPoint();

        int gridY = bounds.Y + 8 + 18 + NameFieldHeight + 10;
        if (statusTimer > 0)
            gridY += 16;

        if (padAnimators != null)
        {
            for (int i = 0; i < ItemsPerPage; i++)
            {
                bool isPressed = false;
                if (mouseDown && !showEmojiPicker)
                {
                    int col = i % ButtonsPerRow;
                    int row = i / ButtonsPerRow;
                    int bx = bounds.X + (bounds.Width - GridWidth) / 2 + col * (ButtonSize + ButtonGap);
                    int by = gridY + row * (ButtonSize + ButtonGap);
                    Rectangle btnRect = new(bx, by, ButtonSize, ButtonSize);
                    isPressed = btnRect.Contains(mouse);
                }
                padAnimators[i].Update(isPressed);
            }
        }

        // Add button
        {
            int addY = bounds.Y + 8 + 18;
            int nameXPos = bounds.X + Padding + EmojiBtnSize + 4;
            int addBtnW = 60;
            int nameW = bounds.X + bounds.Width - Padding - addBtnW - 4 - nameXPos;
            Rectangle addRect = new(nameXPos + nameW + 4, addY, addBtnW, NameFieldHeight);
            addBtnAnimator.Update(mouseDown && addRect.Contains(mouse) && !showEmojiPicker);
        }

        // Nav buttons
        {
            var soundStore = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
            int totalItems = soundStore.Sounds.Count;
            int totalPages = Math.Max(1, (totalItems + ItemsPerPage - 1) / ItemsPerPage);

            if (totalPages > 1)
            {
                int maxRows = (ItemsPerPage + ButtonsPerRow - 1) / ButtonsPerRow;
                int navY = gridY + maxRows * (ButtonSize + ButtonGap) + 2;
                int navCenterX = bounds.X + bounds.Width / 2;

                Rectangle leftBtn = new(navCenterX - 40 - NavButtonSize, navY, NavButtonSize, NavButtonSize);
                Rectangle rightBtn = new(navCenterX + 40, navY, NavButtonSize, NavButtonSize);

                navLeftAnimator.Update(mouseDown && leftBtn.Contains(mouse) && currentPage > 0);
                navRightAnimator.Update(mouseDown && rightBtn.Contains(mouse) && currentPage < totalPages - 1);
            }
            else
            {
                navLeftAnimator.Update(false);
                navRightAnimator.Update(false);
            }
        }

        // Close emoji picker on Escape
        if (showEmojiPicker && Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
        {
            showEmojiPicker = false;
        }

        // Note: playbackController.Update() is called by TerraMainPanel.DraggableUpdate
        // to ensure it runs even when this tab is not in the UI tree.
    }

    public override void SafeMouseDown(UIMouseEvent evt)
    {
        if (!showEmojiPicker || emojiCodepoints == null || emojiPickerCols <= 0) return;

        Point mouse = Main.MouseScreen.ToPoint();
        int visibleHeight = VisibleEmojiRows * EmojiPickerCellSize;
        Rectangle clipRect = new(emojiPickerBounds.X + 4, emojiPickerBounds.Y + 4,
            emojiPickerBounds.Width - 8, visibleHeight);
        Rectangle scrollHitRect = new(clipRect.X + clipRect.Width - EmojiScrollbarHitWidth,
            clipRect.Y, EmojiScrollbarHitWidth, visibleHeight);

        if (!scrollHitRect.Contains(mouse)) return;

        int totalRows = (emojiCodepoints.Length + emojiPickerCols - 1) / emojiPickerCols;
        int maxScrollRows = Math.Max(0, totalRows - VisibleEmojiRows);
        float maxScroll = maxScrollRows * EmojiPickerCellSize;
        if (maxScroll <= 0) return;

        float totalContentHeight = totalRows * EmojiPickerCellSize;
        float barHeight = Math.Max(20, (float)visibleHeight * visibleHeight / totalContentHeight);
        float scrollRatio = emojiScrollOffset / maxScroll;
        float barY = clipRect.Y + scrollRatio * (visibleHeight - barHeight);

        Rectangle thumbRect = new(scrollHitRect.X, (int)barY, scrollHitRect.Width, (int)barHeight);
        emojiScrollDragging = true;

        if (thumbRect.Contains(mouse))
        {
            emojiScrollDragOffsetY = Main.MouseScreen.Y - barY;
        }
        else
        {
            emojiScrollDragOffsetY = barHeight / 2f;
            float trackHeight = visibleHeight - barHeight;
            float mouseRelY = Main.MouseScreen.Y - emojiScrollDragOffsetY - clipRect.Y;
            float ratio = MathHelper.Clamp(mouseRelY / trackHeight, 0f, 1f);
            emojiScrollOffset = ratio * maxScroll;
        }
    }

    public override void SafeClick(UIMouseEvent evt)
    {
        if (contextMenu.HandleLeftClick(Main.MouseScreen.ToPoint())) return;

        Rectangle bounds = GetDimensions().ToRectangle();
        var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
        Point mouse = Main.MouseScreen.ToPoint();

        int y = bounds.Y + 8 + 18;

        // Emoji button
        Rectangle emojiBtnRect = new(bounds.X + Padding, y, EmojiBtnSize, NameFieldHeight);
        if (emojiBtnRect.Contains(mouse))
        {
            showEmojiPicker = !showEmojiPicker;
            if (showEmojiPicker)
                emojiScrollOffset = 0;
            nameFieldFocused = false;
            return;
        }

        // Emoji picker clicks (when open)
        if (showEmojiPicker)
        {
            if (emojiPickerBounds.Width > 0 && emojiPickerBounds.Contains(mouse))
            {
                HandleEmojiPickerClick(mouse);
                return;
            }
            showEmojiPicker = false;
        }

        // Name field
        int nameXPos = emojiBtnRect.X + EmojiBtnSize + 4;
        int addBtnWidth = 60;
        int nameFieldWidth = bounds.X + bounds.Width - Padding - addBtnWidth - 4 - nameXPos;
        Rectangle nameRect = new(nameXPos, y, nameFieldWidth, NameFieldHeight);
        if (nameRect.Contains(mouse))
        {
            nameFieldFocused = true;
            nameInput.SetCursorFromClick(FontAssets.MouseText.Value, 0.6f, nameRect.X + 6, mouse.X);
            return;
        }
        else
        {
            nameFieldFocused = false;
        }

        // Add button
        Rectangle addBtnRect = new(nameRect.X + nameRect.Width + 4, y, addBtnWidth, NameFieldHeight);
        if (addBtnRect.Contains(mouse))
        {
            OnAddClick();
            return;
        }

        y += NameFieldHeight + 10;
        if (statusTimer > 0)
            y += 16;

        // Pagination
        int totalItems = store.Sounds.Count;
        int totalPages = Math.Max(1, (totalItems + ItemsPerPage - 1) / ItemsPerPage);
        int startIndex = currentPage * ItemsPerPage;
        int endIndex = Math.Min(startIndex + ItemsPerPage, totalItems);

        // Sound button clicks (paginated)
        for (int idx = startIndex; idx < endIndex; idx++)
        {
            int localIdx = idx - startIndex;
            int col = localIdx % ButtonsPerRow;
            int row = localIdx / ButtonsPerRow;

            int bx = bounds.X + (bounds.Width - GridWidth) / 2 + col * (ButtonSize + ButtonGap);
            int by = y + row * (ButtonSize + ButtonGap);

            Rectangle btnRect = new(bx, by, ButtonSize, ButtonSize);
            if (btnRect.Contains(mouse))
            {
                playbackController?.PlaySound(store.Sounds[idx].Uuid);
                return;
            }
        }

        // Pagination buttons
        if (totalPages > 1)
        {
            int maxRows = (ItemsPerPage + ButtonsPerRow - 1) / ButtonsPerRow;
            int navY = y + maxRows * (ButtonSize + ButtonGap) + 2;
            int navCenterX = bounds.X + bounds.Width / 2;

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

    private void HandleEmojiPickerClick(Point mouse)
    {
        if (emojiScrollDragging || emojiPickerCols <= 0) return;

        int visibleHeight = VisibleEmojiRows * EmojiPickerCellSize;
        Rectangle clipRect = new(emojiPickerBounds.X + 4, emojiPickerBounds.Y + 4,
            emojiPickerBounds.Width - 8, visibleHeight);

        if (!clipRect.Contains(mouse))
            return;

        int firstRow = (int)emojiScrollOffset / EmojiPickerCellSize;
        int totalRows = (emojiCodepoints.Length + emojiPickerCols - 1) / emojiPickerCols;

        for (int visRow = 0; visRow < VisibleEmojiRows; visRow++)
        {
            int dataRow = firstRow + visRow;
            if (dataRow >= totalRows) break;

            for (int col = 0; col < emojiPickerCols; col++)
            {
                int idx = dataRow * emojiPickerCols + col;
                if (idx >= emojiCodepoints.Length) break;

                int ex = clipRect.X + col * EmojiPickerCellSize;
                int ey = clipRect.Y + visRow * EmojiPickerCellSize;

                Rectangle cellRect = new(ex, ey, EmojiPickerCellSize, EmojiPickerCellSize);
                if (cellRect.Contains(mouse))
                {
                    string emoji = char.ConvertFromUtf32(emojiCodepoints[idx]);
                    nameInput.InsertAtCursor(emoji);
                    showEmojiPicker = false;
                    return;
                }
            }
        }
    }

    public override void SafeRightClick(UIMouseEvent evt)
    {
        if (showEmojiPicker)
        {
            showEmojiPicker = false;
            return;
        }

        if (contextMenu.IsVisible)
        {
            contextMenu.Hide();
            return;
        }

        Rectangle bounds = GetDimensions().ToRectangle();
        var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
        Point mouse = Main.MouseScreen.ToPoint();

        int y = bounds.Y + 8 + 18 + NameFieldHeight + 10;
        if (statusTimer > 0)
            y += 16;

        int totalItems = store.Sounds.Count;
        int startIndex = currentPage * ItemsPerPage;
        int endIndex = Math.Min(startIndex + ItemsPerPage, totalItems);

        for (int idx = startIndex; idx < endIndex; idx++)
        {
            int localIdx = idx - startIndex;
            int col = localIdx % ButtonsPerRow;
            int row = localIdx / ButtonsPerRow;

            int bx = bounds.X + (bounds.Width - GridWidth) / 2 + col * (ButtonSize + ButtonGap);
            int by = y + row * (ButtonSize + ButtonGap);

            Rectangle btnRect = new(bx, by, ButtonSize, ButtonSize);
            if (btnRect.Contains(mouse))
            {
                string uuid = store.Sounds[idx].Uuid;
                string bossLabel  = store.BossSoundUuid  == uuid ? "Boss Sound  [active]" : "Set as Boss Sound";
                string deathLabel = store.DeathSoundUuid == uuid ? "Death Sound [active]" : "Set as Death Sound";
                contextMenu.Show(mouse, new System.Collections.Generic.List<(string, System.Action)>
                {
                    (bossLabel,  () => SetSpecialSound(uuid, isDeath: false)),
                    (deathLabel, () => SetSpecialSound(uuid, isDeath: true)),
                    ("Delete",   () =>
                    {
                        store.RemoveSound(uuid);
                        SetStatus("Sound deleted.");
                        int newTotalPages = Math.Max(1, (store.Sounds.Count + ItemsPerPage - 1) / ItemsPerPage);
                        if (currentPage >= newTotalPages) currentPage = newTotalPages - 1;
                    }),
                });
                return;
            }
        }
    }

    private void OnAddClick()
    {
        if (string.IsNullOrWhiteSpace(nameInput.Text))
        {
            SetStatus("Name required");
            return;
        }

        string name = nameInput.Text.Trim();

        var dialog = new Core.IO.MultiNativeFileDialog();
        ExtensionFilter[] filters = { new("Audio Files", "mp3") };
        string[] files = dialog.OpenFilePanelMulti(filters);

        if (files == null || files.Length == 0)
            return;

        Directory.CreateDirectory(SoundpadDataStore.SoundpadCachePath);

        foreach (string file in files)
        {
            string originalFileName = files.Length > 1 ? Path.GetFileNameWithoutExtension(file) : name;

            FileImportService.ImportFileAsync(
                file,
                SoundpadDataStore.SoundpadCachePath,
                originalFileName,
                "Soundpad",
                "Soundpad",
                result =>
                {
                    Main.QueueMainThreadAction(() =>
                    {
                        var storeInner = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
                        storeInner.AddSound(result.Uuid, originalFileName);

                        int newTotal = storeInner.Sounds.Count;
                        int newTotalPages = Math.Max(1, (newTotal + ItemsPerPage - 1) / ItemsPerPage);
                        currentPage = newTotalPages - 1;
                    });
                },
                error => Main.QueueMainThreadAction(() => SetStatus($"Failed: {originalFileName}")));
        }

        nameInput.Clear();
        nameFieldFocused = false;
    }

    private void SetSpecialSound(string uuid, bool isDeath)
    {
        var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
        var tStore = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();

        if (isDeath)
        {
            store.DeathSoundUuid = uuid;
            tStore.DeathMusicUuid = ""; // mutually exclusive
        }
        else
        {
            store.BossSoundUuid = uuid;
            tStore.BossMusicUuid = ""; // mutually exclusive
        }
        store.ForceSave();
        tStore.ForceSave();

        if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
        {
            if (isDeath)
                Networking.PacketBuilder.SetDeathSoundpad((byte)Main.myPlayer, uuid).Send();
            else
                Networking.PacketBuilder.SetBossSoundpad((byte)Main.myPlayer, uuid).Send();
        }
    }

    private void SetStatus(string message)
    {
        statusMessage = message;
        statusTimer = 180; // ~3 seconds
    }

}
