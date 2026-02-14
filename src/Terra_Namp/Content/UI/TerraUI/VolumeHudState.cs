using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Components;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.UI;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI;

public class VolumeHudState : SmartUIState
{
    private float displayVolume;
    private int showTimer;
    private int slideInTick;

    private const int ShowDuration = 120; // 2 seconds at 60fps
    private const int SlideInTicks = 8;
    private const int FadeOutTicks = 20;
    private const int HudWidth = 180;
    private const int HudHeight = 28;
    private const int BarHeight = 4;

    public override int InsertionIndex(List<GameInterfaceLayer> layers) => layers.Count - 1;

    public override void OnInitialize() => Visible = true;

    public static void Show(float volume)
    {
        var state = TerraUILoader.GetUIState<VolumeHudState>();
        if (state != null)
        {
            state.displayVolume = volume;
            // Only start slide-in animation if HUD is not already visible
            if (state.showTimer <= 0)
                state.slideInTick = 0;
            state.showTimer = ShowDuration;
        }
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        if (showTimer > 0)
            showTimer--;

        if (slideInTick < SlideInTicks)
            slideInTick++;

        base.SafeUpdate(gameTime);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (showTimer <= 0) return;

        // Animation
        float alpha;
        float yOffset;

        if (slideInTick < SlideInTicks)
        {
            float t = slideInTick / (float)SlideInTicks;
            t = t * t * (3 - 2 * t); // smoothstep ease
            alpha = t;
            yOffset = MathHelper.Lerp(-(HudHeight + 16), 0, t);
        }
        else if (showTimer < FadeOutTicks)
        {
            float t = showTimer / (float)FadeOutTicks;
            t = t * t * (3 - 2 * t);
            alpha = t;
            yOffset = MathHelper.Lerp(-HudHeight / 3f, 0, t);
        }
        else
        {
            alpha = 1f;
            yOffset = 0;
        }

        float centerX = Main.screenWidth / 2f;
        float y = 16 + yOffset;

        var font = FontAssets.MouseText.Value;
        Color accent = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().PanelColor;

        // Background
        Rectangle bgRect = new((int)(centerX - HudWidth / 2f), (int)y, HudWidth, HudHeight);
        DrawingUtils.DrawRoundedRect(spriteBatch, bgRect, Color.Black * 0.8f * alpha, 8);
        DrawingUtils.DrawRoundedBorder(spriteBatch, bgRect, accent * 0.3f * alpha, 8);

        // Volume percentage (left side)
        string text = $"{(int)(displayVolume * 100)}%";
        float textScale = 0.55f;
        Vector2 textSize = font.MeasureString(text) * textScale;
        spriteBatch.DrawString(font, text,
            new Vector2(bgRect.X + 10, bgRect.Y + (HudHeight - textSize.Y) / 2f),
            Color.White * 0.9f * alpha, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

        // Bar (right side)
        int barX = bgRect.X + 48;
        int barWidth = HudWidth - 58;
        float barY = bgRect.Y + (HudHeight - BarHeight) / 2f;

        Rectangle barBg = new(barX, (int)barY, barWidth, BarHeight);
        DrawingUtils.DrawRoundedRect(spriteBatch, barBg, Color.White * 0.15f * alpha, 2);

        int fillWidth = (int)(barWidth * displayVolume);
        if (fillWidth > 0)
        {
            Rectangle barFill = new(barX, (int)barY, fillWidth, BarHeight);
            DrawingUtils.DrawRoundedRect(spriteBatch, barFill, accent * alpha, 2);
        }
    }
}
