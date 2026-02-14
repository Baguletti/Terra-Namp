using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Enums;
using Terra_Namp.Core.IO;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using ReLogic.Content;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class Visualizer : SmartUIElement
{
    private const int BarCount = 48;
    private const int SmoothSampleCount = 200;
    private const float SmoothingFactor = 0.25f; // For bars mode
    private const float SmoothModeSmoothingFactor = 0.15f; // Slower smoothing for wave mode (less jittery)
    private const float SmoothModeAmplitudeMultiplier = 1.5f; // Boost peaks for wave mode
    private const int SmoothModeLineThickness = 2; // Draw thicker line (2-3 pixels wide)
    private const float ReflectionDarken = 0.30f; // Darken reflection by 30%
    private const float ReflectionPerspective = 0.25f; // Perspective skew amount (25%)

    private readonly float[] smoothedHeights = new float[SmoothSampleCount];

    public byte[] AudioData { get; set; }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        byte[] data = AudioData;

        if (data == null || data.Length == 0)
        {
            base.Draw(spriteBatch);
            return;
        }

        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();

        // Choose visualization type
        if (store.VisualizerType == VisualizerType.Smooth)
            DrawSmoothWavePixelBased(spriteBatch, bounds, data, store);
        else
            DrawBars(spriteBatch, bounds, data, store);

        base.Draw(spriteBatch);
    }

    private void DrawBars(SpriteBatch spriteBatch, Rectangle bounds, byte[] data, TerraDataStore store)
    {
        Color accentColor = store.PanelColor;
        Color secondaryColor = store.SecondaryColor;

        int gap = 2;
        int barWidth = Math.Max(1, (bounds.Width - (BarCount - 1) * gap) / BarCount);
        int totalBarWidth = barWidth + gap;
        int startX = bounds.X + (bounds.Width - totalBarWidth * BarCount + gap) / 2;
        int centerY = bounds.Y + bounds.Height / 2;

        for (int i = 0; i < BarCount; i++)
        {
            int sampleStart = (int)((float)i / BarCount * data.Length);
            int sampleLength = Math.Max(1, data.Length / BarCount);
            float sample = Average(data, sampleStart, sampleLength) / 255f;

            smoothedHeights[i] = MathHelper.Lerp(smoothedHeights[i], sample, SmoothingFactor);

            int halfHeight = (int)(smoothedHeights[i] * (bounds.Height / 2 - 2)) + 1;
            int x = startX + i * totalBarWidth;

            // Gradient from secondary color to accent color
            float t = (float)i / (BarCount - 1);
            Color barColor = Color.Lerp(secondaryColor, accentColor, t);

            // Top half (mirrored)
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(x, centerY - halfHeight, barWidth, halfHeight),
                barColor * 0.8f);

            // Bottom half (reflection with darkening)
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(x, centerY, barWidth, halfHeight),
                barColor * (0.6f - ReflectionDarken));
        }
    }

    private void DrawSmoothWavePixelBased(SpriteBatch spriteBatch, Rectangle bounds, byte[] data, TerraDataStore store)
    {
        Color accentColor = store.PanelColor;
        Color secondaryColor = store.SecondaryColor;

        // Use fewer sample points for smoother interpolation
        int baseSampleCount = 64;
        int centerY = bounds.Y + bounds.Height / 2;

        // Leave more room for peaks - use 80% of available space
        float maxAmplitudeBase = (bounds.Height / 2) * 0.8f;

        // Sample audio data
        float[] samples = new float[baseSampleCount];
        float peakValue = 0f;

        for (int i = 0; i < baseSampleCount; i++)
        {
            int sampleStart = (int)((float)i / baseSampleCount * data.Length);
            int sampleLength = Math.Max(1, data.Length / baseSampleCount);
            float sample = Average(data, sampleStart, sampleLength) / 255f;

            // Apply temporal smoothing (slower for less jitter in wave mode)
            if (i < smoothedHeights.Length)
                smoothedHeights[i] = MathHelper.Lerp(smoothedHeights[i], sample, SmoothModeSmoothingFactor);

            float smoothedValue = i < smoothedHeights.Length ? smoothedHeights[i] : sample;

            // Boost amplitude for more prominent peaks
            smoothedValue = Math.Min(1f, smoothedValue * SmoothModeAmplitudeMultiplier);

            samples[i] = smoothedValue;

            // Track peak for dynamic scaling
            if (smoothedValue > peakValue)
                peakValue = smoothedValue;
        }

        // Dynamic amplitude scaling to prevent clipping
        // If peak would clip, scale down to fit
        float amplitudeScale = 1f;
        if (peakValue > 0.01f) // Avoid division by zero
        {
            // Add 10% safety margin
            float targetPeak = 0.9f;
            if (peakValue > targetPeak)
                amplitudeScale = targetPeak / peakValue;
        }

        float maxAmplitude = maxAmplitudeBase * amplitudeScale;

        // Draw with interpolation for every pixel
        for (int x = 0; x < bounds.Width; x++)
        {
            float t = (float)x / bounds.Width;

            // Get interpolated amplitude using Catmull-Rom spline
            float samplePos = t * (baseSampleCount - 1);
            int sampleIndex = (int)samplePos;
            float localT = samplePos - sampleIndex;

            float amplitude = GetInterpolatedValue(samples, baseSampleCount, sampleIndex, localT) * maxAmplitude;

            // Gradient color
            Color waveColor = Color.Lerp(secondaryColor, accentColor, t);

            int pixelX = bounds.X + x;
            int waveHeight = (int)amplitude;
            float fractionalPart = amplitude - waveHeight; // For antialiasing

            // Draw thicker line (multiple pixels wide) for better visibility
            for (int offsetX = 0; offsetX < SmoothModeLineThickness; offsetX++)
            {
                if (pixelX + offsetX >= bounds.X + bounds.Width) break;

                // Draw filled wave with gradient alpha
                for (int y = 0; y <= waveHeight; y++)
                {
                    float yRatio = waveHeight > 0 ? (float)y / waveHeight : 0;
                    float alpha = 1f - yRatio * 0.5f; // Fade from solid to 50% transparent

                    // Top half (original wave)
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                        new Rectangle(pixelX + offsetX, centerY - y, 1, 1),
                        waveColor * alpha * 0.8f);

                    // Bottom half (reflection with perspective skew and darkening)
                    int perspectiveOffset = (int)(y * ReflectionPerspective); // Skew to the right
                    float reflectionAlpha = alpha * (0.6f - ReflectionDarken);

                    spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                        new Rectangle(pixelX + offsetX + perspectiveOffset, centerY + y, 1, 1),
                        waveColor * reflectionAlpha);
                }
            }

            // Antialiasing: draw multiple edge pixels with gradient alpha for smooth borders
            if (waveHeight < maxAmplitude - 2)
            {
                // Draw 2-3 antialiased edge pixels for smoother appearance
                float baseEdgeAlpha = Math.Max(0.3f, fractionalPart);

                for (int offsetX = 0; offsetX < SmoothModeLineThickness; offsetX++)
                {
                    if (pixelX + offsetX >= bounds.X + bounds.Width) break;

                    // First edge pixel (strongest)
                    // Top edge
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                        new Rectangle(pixelX + offsetX, centerY - waveHeight - 1, 1, 1),
                        waveColor * baseEdgeAlpha * 0.7f);

                    // Bottom edge (reflection with perspective and darkening)
                    int edgePerspective1 = (int)((waveHeight + 1) * ReflectionPerspective);
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                        new Rectangle(pixelX + offsetX + edgePerspective1, centerY + waveHeight + 1, 1, 1),
                        waveColor * baseEdgeAlpha * (0.5f - ReflectionDarken));

                    // Second edge pixel (softer fade)
                    if (baseEdgeAlpha > 0.4f)
                    {
                        // Top edge
                        spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                            new Rectangle(pixelX + offsetX, centerY - waveHeight - 2, 1, 1),
                            waveColor * baseEdgeAlpha * 0.3f);

                        // Bottom edge (reflection with perspective and darkening)
                        int edgePerspective2 = (int)((waveHeight + 2) * ReflectionPerspective);
                        spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                            new Rectangle(pixelX + offsetX + edgePerspective2, centerY + waveHeight + 2, 1, 1),
                            waveColor * baseEdgeAlpha * (0.2f - ReflectionDarken * 0.5f));
                    }
                }
            }
        }
    }

    private static float GetInterpolatedValue(float[] samples, int count, int index, float t)
    {
        // Catmull-Rom spline interpolation for smooth curves
        if (count < 4) return samples[Math.Clamp(index, 0, count - 1)];

        int p0 = Math.Clamp(index - 1, 0, count - 1);
        int p1 = Math.Clamp(index, 0, count - 1);
        int p2 = Math.Clamp(index + 1, 0, count - 1);
        int p3 = Math.Clamp(index + 2, 0, count - 1);

        float v0 = samples[p0];
        float v1 = samples[p1];
        float v2 = samples[p2];
        float v3 = samples[p3];

        // Catmull-Rom formula
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * v1) +
            (-v0 + v2) * t +
            (2f * v0 - 5f * v1 + 4f * v2 - v3) * t2 +
            (-v0 + 3f * v1 - 3f * v2 + v3) * t3
        );
    }

    private static float Average(byte[] array, int start, int length)
    {
        if (length <= 0) return 0;
        int total = 0;
        int end = Math.Min(start + length, array.Length);
        for (int i = start; i < end; i++)
            total += array[i];
        return (float)total / (end - start);
    }

}
