using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

namespace Terra_Namp.Content.UI.NowPlayingUI
{
    public class NowPlayingPopup : SmartUIElement
    {
        public bool IsCompleted { get; private set; }

        private readonly string name;
        private readonly string author;
        private readonly int popupDuration;

        private const int PopupWidth = 280;
        private const int PopupHeight = 64;
        private const int Padding = 10;

        private readonly int maxOffset;
        private readonly TextBanner[] banners;
        private readonly Rectangle[] bannerRectangles;

        private State drawState;
        private int xOffset;
        private int waitTimer;
        private float textFade;

        public NowPlayingPopup(string name, string author, bool startAlreadyOpen = false)
        {
            this.name = name;
            this.author = author;

            popupDuration = Math.Max((int)(name.Length * 0.2f * 60), 5 * 60);

            maxOffset = -(PopupWidth + 32);
            xOffset = maxOffset;

            if (startAlreadyOpen)
            {
                xOffset = 0;
                drawState = State.Waiting;
            }

            banners = new TextBanner[2];
            bannerRectangles = new Rectangle[2];
        }

        public override void SafeUpdate(GameTime gameTime)
        {
            TerraMainPanel panel = TerraUILoader.GetUIState<TerraState>().MainPanel;

            if (panel.ActiveSong == null)
            {
                drawState = State.Closing;
            }

            switch (drawState)
            {
                case State.Opening:

                    xOffset += 20;

                    if (xOffset >= 0)
                    {
                        xOffset = 0;

                        drawState = State.Waiting;
                    }

                    break;
                case State.Waiting:

                    waitTimer++;
                    

                    if (waitTimer > popupDuration)
                    {
                        drawState = State.Closing;
                    }

                    textFade += 0.02f;

                    if (textFade >= 1)
                    {
                        textFade = 1;
                    }

                    break;
                case State.Closing:

                    xOffset -= 20;

                    if (xOffset <= maxOffset)
                    {
                        xOffset = maxOffset;

                        IsCompleted = true;
                    }

                    break;
            }

            if (banners[0] == null)
            {
                var font = FontAssets.MouseText.Value;
                float scale = 0.7f;
                int lineHeight = (int)(font.MeasureString("A").Y * scale);

                Vector2 drawPosition = new(32, Main.screenHeight - PopupHeight - 32);
                int drawY = (int)drawPosition.Y + Padding + 8;
                int rectWidth = PopupWidth - Padding * 2;

                Rectangle nameRectangle = new((int)drawPosition.X + Padding, drawY, rectWidth, lineHeight);
                banners[0] = new TextBanner(name, nameRectangle, font, scale);
                bannerRectangles[0] = nameRectangle;

                nameRectangle.Y += lineHeight + 4;
                banners[1] = new TextBanner(author, nameRectangle, font, scale);
                bannerRectangles[1] = nameRectangle;
            }

            for (int i = 0; i < banners.Length; i++)
            {
                banners[i]?.UpdateScrolling();
            }

            base.SafeUpdate(gameTime);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            var font = FontAssets.MouseText.Value;

            Rectangle drawBox = new(32 + xOffset, Main.screenHeight - PopupHeight - 32, PopupWidth, PopupHeight);
            float progress = 1 - ((float)xOffset / maxOffset);
            Color accentColor = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;
            Color backgroundColor = store.PanelBackgroundColor;
            float opacity = store.PanelOpacity * progress;
            int cornerRadius = store.CornerRadius;

            // Blur background
            if (store.BlurLevel > 0)
            {
                BlurHelper.DrawBlurredBackground(spriteBatch, drawBox, store.BlurLevel, cornerRadius);
            }

            // Panel background
            DrawingUtils.DrawRoundedRect(spriteBatch, drawBox, backgroundColor * opacity, cornerRadius);

            // Border
            DrawingUtils.DrawRoundedBorder(spriteBatch, drawBox, accentColor * 0.3f * progress, cornerRadius);

            // "Now Playing" label
            string label = "Now Playing";
            float labelScale = 0.55f;
            Vector2 labelPos = new(drawBox.X + Padding, drawBox.Y + 4);
            spriteBatch.DrawString(font, label, labelPos, accentColor * 0.6f * progress,
                0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            // Song title and author (scrolling text)
            if (banners[0] != null && drawState == State.Waiting)
            {
                for (int i = 0; i < banners.Length; i++)
                {
                    // Update rectangle to current position (popup moves with xOffset)
                    Rectangle currentRect = bannerRectangles[i];
                    currentRect.X += xOffset;
                    banners[i].UpdateRectangle(currentRect);

                    Vector2 position = bannerRectangles[i].TopLeft() + new Vector2(xOffset, 0);
                    Color textColor = i == 0 ? accentColor : store.SecondaryColor * 0.7f;
                    banners[i].Draw(spriteBatch, position, textColor * textFade * progress);
                }
            }

            base.Draw(spriteBatch);
        }

        private enum State
        {
            Opening,
            Waiting,
            Closing
        }
    }
}
