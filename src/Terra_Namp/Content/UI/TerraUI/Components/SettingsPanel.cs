using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Enums;
using Terra_Namp.Core.IO;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI.Components;

public class SettingsPanel : ScrollablePanel
{
    private const int Padding = 10;

    private HorizontalSlider opacitySlider;
    private HorizontalSlider blurSlider;
    private HorizontalSlider cornerRadiusSlider;
    private ColorPicker accentPicker;
    private ColorPicker secondaryPicker;
    private ColorPicker backgroundPicker;
    private UIText visualizerLabel;
    private ControlButton visualizerButton;
    private UIText miniPlayerLabel;
    private ControlButton miniPlayerButton;

    private UIText volumeOverrideLabel;
    private ControlButton volumeOverrideButton;
    private HorizontalSlider soundVolumeSlider;
    private HorizontalSlider ambientVolumeSlider;

    private int totalContentHeight;

    protected override int GetTotalContentHeight() => totalContentHeight;

    public override void OnInitialize()
    {
        OverflowHidden = true;

        var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
        int contentWidth = TerraMainPanel.PanelWidth - Padding * 2;
        int y = 10;

        // --- Opacity slider ---
        opacitySlider = new HorizontalSlider
        {
            Label = "Opacity",
            Value = store.PanelOpacity,
            MinValue = 0.1f,
            MaxValue = 1f,
            FormatValue = v => $"{(int)(v * 100)}%"
        };
        opacitySlider.Left.Set(Padding, 0);
        opacitySlider.Top.Set(y, 0);
        opacitySlider.Width.Set(contentWidth, 0);
        opacitySlider.Height.Set(30, 0);
        opacitySlider.OnValueChanged += v =>
        {
            store.PanelOpacity = v;
            store.ForceSave();
        };
        Append(opacitySlider);
        y += 40;

        // --- Blur slider ---
        blurSlider = new HorizontalSlider
        {
            Label = "Blur",
            Value = store.BlurLevel,
            MinValue = 0f,
            MaxValue = 10f,
            Steps = 10,
            FormatValue = v => $"{(int)v}"
        };
        blurSlider.Left.Set(Padding, 0);
        blurSlider.Top.Set(y, 0);
        blurSlider.Width.Set(contentWidth, 0);
        blurSlider.Height.Set(30, 0);
        blurSlider.OnValueChanged += v =>
        {
            store.BlurLevel = (int)v;
            store.ForceSave();
        };
        Append(blurSlider);
        y += 40;

        // --- Corner Radius slider ---
        cornerRadiusSlider = new HorizontalSlider
        {
            Label = "Corner Radius",
            Value = store.CornerRadius,
            MinValue = 0f,
            MaxValue = 12f,
            Steps = 12,
            FormatValue = v => $"{(int)v}px"
        };
        cornerRadiusSlider.Left.Set(Padding, 0);
        cornerRadiusSlider.Top.Set(y, 0);
        cornerRadiusSlider.Width.Set(contentWidth, 0);
        cornerRadiusSlider.Height.Set(30, 0);
        cornerRadiusSlider.OnValueChanged += v =>
        {
            store.CornerRadius = (int)v;
            store.ForceSave();
        };
        Append(cornerRadiusSlider);
        y += 50;

        // --- Accent Color Picker ---
        accentPicker = new ColorPicker
        {
            Label = "Accent Color"
        };
        accentPicker.SetColor(store.PanelColor);
        accentPicker.Left.Set(Padding, 0);
        accentPicker.Top.Set(y, 0);
        accentPicker.Width.Set(contentWidth, 0);
        accentPicker.Height.Set(105, 0);
        accentPicker.OnColorChanged += color =>
        {
            store.PanelColor = color;
            store.ForceSave();
        };
        Append(accentPicker);
        y += 110;

        // --- Secondary Color Picker ---
        secondaryPicker = new ColorPicker
        {
            Label = "Secondary Color"
        };
        secondaryPicker.SetColor(store.SecondaryColor);
        secondaryPicker.Left.Set(Padding, 0);
        secondaryPicker.Top.Set(y, 0);
        secondaryPicker.Width.Set(contentWidth, 0);
        secondaryPicker.Height.Set(105, 0);
        secondaryPicker.OnColorChanged += color =>
        {
            store.SecondaryColor = color;
            store.ForceSave();
        };
        Append(secondaryPicker);
        y += 110;

        // --- Background Color Picker ---
        backgroundPicker = new ColorPicker
        {
            Label = "Background Color"
        };
        backgroundPicker.SetColor(store.PanelBackgroundColor);
        backgroundPicker.Left.Set(Padding, 0);
        backgroundPicker.Top.Set(y, 0);
        backgroundPicker.Width.Set(contentWidth, 0);
        backgroundPicker.Height.Set(105, 0);
        backgroundPicker.OnColorChanged += color =>
        {
            store.PanelBackgroundColor = color;
            store.ForceSave();
        };
        Append(backgroundPicker);
        y += 110;

        // --- Visualizer Type ---
        visualizerLabel = new UIText("Visualizer Type:", 0.8f);
        visualizerLabel.Left.Set(Padding, 0);
        visualizerLabel.Top.Set(y, 0);
        Append(visualizerLabel);
        y += 25;

        visualizerButton = new ControlButton(GetVisualizerTypeText(store.VisualizerType));
        visualizerButton.Left.Set(Padding, 0);
        visualizerButton.Top.Set(y, 0);
        visualizerButton.Width.Set(contentWidth, 0);
        visualizerButton.Height.Set(30, 0);
        visualizerButton.OnLeftClick += (evt, args) =>
        {
            store.VisualizerType = store.VisualizerType == VisualizerType.Bars ? VisualizerType.Smooth : VisualizerType.Bars;
            visualizerButton.SetText(GetVisualizerTypeText(store.VisualizerType));
            store.ForceSave();
        };
        Append(visualizerButton);
        y += 45;

        // --- Mini Player Toggle ---
        miniPlayerLabel = new UIText("Mini Player:", 0.8f);
        miniPlayerLabel.Left.Set(Padding, 0);
        miniPlayerLabel.Top.Set(y, 0);
        Append(miniPlayerLabel);
        y += 25;

        miniPlayerButton = new ControlButton(store.MiniPlayerEnabled ? "Enabled" : "Disabled");
        miniPlayerButton.Left.Set(Padding, 0);
        miniPlayerButton.Top.Set(y, 0);
        miniPlayerButton.Width.Set(contentWidth, 0);
        miniPlayerButton.Height.Set(30, 0);
        miniPlayerButton.OnLeftClick += (evt, args) =>
        {
            store.MiniPlayerEnabled = !store.MiniPlayerEnabled;
            miniPlayerButton.SetText(store.MiniPlayerEnabled ? "Enabled" : "Disabled");
            store.ForceSave();
        };
        Append(miniPlayerButton);
        y += 45;

        // --- Volume Override Toggle ---
        volumeOverrideLabel = new UIText("Override Game Volume:", 0.8f);
        volumeOverrideLabel.Left.Set(Padding, 0);
        volumeOverrideLabel.Top.Set(y, 0);
        Append(volumeOverrideLabel);
        y += 25;

        volumeOverrideButton = new ControlButton(store.VolumeOverrideEnabled ? "Enabled" : "Disabled");
        volumeOverrideButton.Left.Set(Padding, 0);
        volumeOverrideButton.Top.Set(y, 0);
        volumeOverrideButton.Width.Set(contentWidth, 0);
        volumeOverrideButton.Height.Set(30, 0);
        volumeOverrideButton.OnLeftClick += (evt, args) =>
        {
            store.VolumeOverrideEnabled = !store.VolumeOverrideEnabled;
            volumeOverrideButton.SetText(store.VolumeOverrideEnabled ? "Enabled" : "Disabled");
            ToggleVolumeSliders(store.VolumeOverrideEnabled);
            store.ForceSave();
        };
        Append(volumeOverrideButton);
        y += 35;

        // --- Sound Volume slider ---
        soundVolumeSlider = new HorizontalSlider
        {
            Label = "Sound Volume",
            Value = store.SoundVolumeLevel,
            MinValue = 0f,
            MaxValue = 1f,
            FormatValue = v => $"{(int)(v * 100)}%"
        };
        soundVolumeSlider.Left.Set(Padding, 0);
        soundVolumeSlider.Top.Set(y, 0);
        soundVolumeSlider.Width.Set(contentWidth, 0);
        soundVolumeSlider.Height.Set(30, 0);
        soundVolumeSlider.OnValueChanged += v =>
        {
            store.SoundVolumeLevel = v;
            store.ForceSave();
        };
        y += 40;

        // --- Ambient Volume slider ---
        ambientVolumeSlider = new HorizontalSlider
        {
            Label = "Ambient Volume",
            Value = store.AmbientVolumeLevel,
            MinValue = 0f,
            MaxValue = 1f,
            FormatValue = v => $"{(int)(v * 100)}%"
        };
        ambientVolumeSlider.Left.Set(Padding, 0);
        ambientVolumeSlider.Top.Set(y, 0);
        ambientVolumeSlider.Width.Set(contentWidth, 0);
        ambientVolumeSlider.Height.Set(30, 0);
        ambientVolumeSlider.OnValueChanged += v =>
        {
            store.AmbientVolumeLevel = v;
            store.ForceSave();
        };
        y += 40;

        // Only append sliders if override is enabled
        if (store.VolumeOverrideEnabled)
        {
            Append(soundVolumeSlider);
            Append(ambientVolumeSlider);
        }

        totalContentHeight = y + 10;
    }

    private void ToggleVolumeSliders(bool show)
    {
        if (show)
        {
            Append(soundVolumeSlider);
            Append(ambientVolumeSlider);
        }
        else
        {
            RemoveChild(soundVolumeSlider);
            RemoveChild(ambientVolumeSlider);
        }
        RecalculateLayout();
    }

    private void RecalculateLayout()
    {
        soundVolumeSlider?.Activate();
        ambientVolumeSlider?.Activate();
        Recalculate();
    }

    private static string GetVisualizerTypeText(VisualizerType type)
    {
        return type switch
        {
            VisualizerType.Bars => "Bars",
            VisualizerType.Smooth => "Smooth Wave",
            _ => "Bars"
        };
    }

    public override void Recalculate()
    {
        int baseY = 10;

        if (opacitySlider != null)
            opacitySlider.Top.Set(baseY - ScrollOffset, 0);
        baseY += 40;

        if (blurSlider != null)
            blurSlider.Top.Set(baseY - ScrollOffset, 0);
        baseY += 40;

        if (cornerRadiusSlider != null)
            cornerRadiusSlider.Top.Set(baseY - ScrollOffset, 0);
        baseY += 50;

        if (accentPicker != null)
            accentPicker.Top.Set(baseY - ScrollOffset, 0);
        baseY += 110;

        if (secondaryPicker != null)
            secondaryPicker.Top.Set(baseY - ScrollOffset, 0);
        baseY += 110;

        if (backgroundPicker != null)
            backgroundPicker.Top.Set(baseY - ScrollOffset, 0);
        baseY += 110;

        if (visualizerLabel != null)
            visualizerLabel.Top.Set(baseY - ScrollOffset, 0);
        baseY += 25;

        if (visualizerButton != null)
            visualizerButton.Top.Set(baseY - ScrollOffset, 0);
        baseY += 45;

        if (miniPlayerLabel != null)
            miniPlayerLabel.Top.Set(baseY - ScrollOffset, 0);
        baseY += 25;

        if (miniPlayerButton != null)
            miniPlayerButton.Top.Set(baseY - ScrollOffset, 0);
        baseY += 45;

        if (volumeOverrideLabel != null)
            volumeOverrideLabel.Top.Set(baseY - ScrollOffset, 0);
        baseY += 25;

        if (volumeOverrideButton != null)
            volumeOverrideButton.Top.Set(baseY - ScrollOffset, 0);
        baseY += 35;

        bool overrideOn = PersistentDataStoreSystem.GetDataStore<TerraDataStore>().VolumeOverrideEnabled;

        if (overrideOn && soundVolumeSlider != null)
            soundVolumeSlider.Top.Set(baseY - ScrollOffset, 0);
        if (overrideOn)
            baseY += 40;

        if (overrideOn && ambientVolumeSlider != null)
            ambientVolumeSlider.Top.Set(baseY - ScrollOffset, 0);
        if (overrideOn)
            baseY += 40;

        totalContentHeight = baseY + 10;

        base.Recalculate();
    }
}
