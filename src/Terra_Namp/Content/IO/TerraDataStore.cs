using Microsoft.Xna.Framework;
using Terra_Namp.Content.UI.TerraUI.Enums;
using Terra_Namp.Core.IO;
using Terraria.ModLoader.IO;

namespace Terra_Namp.Content.IO
{
    public class TerraDataStore : PersistentDataStore
    {
        private const string PlayModeTag = "Terra_Namp:PlayMode";
        private const string VolumeLevelTag = "Terra_Namp:VolumeLevel";
        private const string VolumeAngleTag = "Terra_Namp:VolumeAngle"; // legacy
        private const string WindowPosXTag = "Terra_Namp:WindowPosX";
        private const string WindowPosYTag = "Terra_Namp:WindowPosY";
        private const string PanelOpacityTag = "Terra_Namp:PanelOpacity";
        private const string BlurLevelTag = "Terra_Namp:BlurLevel";
        private const string PanelColorRTag = "Terra_Namp:PanelColorR";
        private const string PanelColorGTag = "Terra_Namp:PanelColorG";
        private const string PanelColorBTag = "Terra_Namp:PanelColorB";
        private const string PanelBgRTag = "Terra_Namp:PanelBgR";
        private const string PanelBgGTag = "Terra_Namp:PanelBgG";
        private const string PanelBgBTag = "Terra_Namp:PanelBgB";
        private const string SecondaryColorRTag = "Terra_Namp:SecondaryColorR";
        private const string SecondaryColorGTag = "Terra_Namp:SecondaryColorG";
        private const string SecondaryColorBTag = "Terra_Namp:SecondaryColorB";
        private const string CornerRadiusTag = "Terra_Namp:CornerRadius";
        private const string VisualizerTypeTag = "Terra_Namp:VisualizerType";
        private const string MiniPlayerEnabledTag = "Terra_Namp:MiniPlayerEnabled";
        private const string VolumeOverrideEnabledTag = "Terra_Namp:VolumeOverrideEnabled";
        private const string SoundVolumeLevelTag = "Terra_Namp:SoundVolumeLevel";
        private const string AmbientVolumeLevelTag = "Terra_Namp:AmbientVolumeLevel";
        private const string OriginalSoundVolumeTag = "Terra_Namp:OriginalSoundVolume";
        private const string OriginalAmbientVolumeTag = "Terra_Namp:OriginalAmbientVolume";
        // Boss/death UUIDs intentionally NOT persisted — session-only, server resets on recompile

        public PlayMode PlayMode { get; set; }
        public VisualizerType VisualizerType { get; set; } = VisualizerType.Bars;
        public bool MiniPlayerEnabled { get; set; } = false;

        public bool VolumeOverrideEnabled { get; set; }
        public float SoundVolumeLevel { get; set; } = 1f;
        public float AmbientVolumeLevel { get; set; } = 1f;

        public float OriginalSoundVolume { get; set; } = -1f;
        public float OriginalAmbientVolume { get; set; } = -1f;

        public string BossMusicUuid { get; set; } = "";
        public string DeathMusicUuid { get; set; } = "";

        public float VolumeLevel { get; set; } = 0.5f;

        public float WindowPositionX { get; set; } = 0.65f;
        public float WindowPositionY { get; set; } = 0.5f;

        public float PanelOpacity { get; set; } = 0.6f;
        public int BlurLevel { get; set; } = 10;
        public int CornerRadius { get; set; } = 6;
        public Color PanelColor { get; set; } = new Color(204, 120, 52);
        public Color SecondaryColor { get; set; } = new Color(140, 0, 235);
        public Color PanelBackgroundColor { get; set; } = Color.Black;

        public override string FileName => "playback_preferences.dat";

        public override void LoadGlobal(TagCompound tag)
        {
            if (tag.ContainsKey(PlayModeTag))
            {
                PlayMode = (PlayMode)tag.GetInt(PlayModeTag);
            }

            if (tag.ContainsKey(VisualizerTypeTag))
            {
                VisualizerType = (VisualizerType)tag.GetInt(VisualizerTypeTag);
            }

            if (tag.ContainsKey(VolumeLevelTag))
            {
                VolumeLevel = tag.GetFloat(VolumeLevelTag);
            }
            else if (tag.ContainsKey(VolumeAngleTag))
            {
                // Backward compat: convert old angle to 0-1 range.
                float angle = tag.GetFloat(VolumeAngleTag);
                float minAngle = -(MathHelper.PiOver4);
                float maxAngle = MathHelper.Pi + MathHelper.PiOver4;
                VolumeLevel = MathHelper.Clamp((angle - minAngle) / (maxAngle - minAngle), 0f, 1f);
            }

            if (tag.ContainsKey(WindowPosXTag))
                WindowPositionX = tag.GetFloat(WindowPosXTag);
            if (tag.ContainsKey(WindowPosYTag))
                WindowPositionY = tag.GetFloat(WindowPosYTag);

            if (tag.ContainsKey(PanelOpacityTag))
                PanelOpacity = tag.GetFloat(PanelOpacityTag);
            if (tag.ContainsKey(BlurLevelTag))
                BlurLevel = tag.GetInt(BlurLevelTag);
            if (tag.ContainsKey(CornerRadiusTag))
                CornerRadius = tag.GetInt(CornerRadiusTag);

            if (tag.ContainsKey(PanelColorRTag))
            {
                byte r = tag.GetByte(PanelColorRTag);
                byte g = tag.GetByte(PanelColorGTag);
                byte b = tag.GetByte(PanelColorBTag);
                PanelColor = new Color(r, g, b);
            }

            if (tag.ContainsKey(PanelBgRTag))
            {
                byte r = tag.GetByte(PanelBgRTag);
                byte g = tag.GetByte(PanelBgGTag);
                byte b = tag.GetByte(PanelBgBTag);
                PanelBackgroundColor = new Color(r, g, b);
            }

            if (tag.ContainsKey(SecondaryColorRTag))
            {
                byte r = tag.GetByte(SecondaryColorRTag);
                byte g = tag.GetByte(SecondaryColorGTag);
                byte b = tag.GetByte(SecondaryColorBTag);
                SecondaryColor = new Color(r, g, b);
            }

            if (tag.ContainsKey(MiniPlayerEnabledTag))
                MiniPlayerEnabled = tag.GetBool(MiniPlayerEnabledTag);

            if (tag.ContainsKey(VolumeOverrideEnabledTag))
                VolumeOverrideEnabled = tag.GetBool(VolumeOverrideEnabledTag);
            if (tag.ContainsKey(SoundVolumeLevelTag))
                SoundVolumeLevel = tag.GetFloat(SoundVolumeLevelTag);
            if (tag.ContainsKey(AmbientVolumeLevelTag))
                AmbientVolumeLevel = tag.GetFloat(AmbientVolumeLevelTag);
            if (tag.ContainsKey(OriginalSoundVolumeTag))
                OriginalSoundVolume = tag.GetFloat(OriginalSoundVolumeTag);
            if (tag.ContainsKey(OriginalAmbientVolumeTag))
                OriginalAmbientVolume = tag.GetFloat(OriginalAmbientVolumeTag);
            // BossMusicUuid / DeathMusicUuid not loaded — session-only
        }

        public override void SaveGlobal(TagCompound tag)
        {
            tag[PlayModeTag] = (int)PlayMode;
            tag[VisualizerTypeTag] = (int)VisualizerType;
            tag[VolumeLevelTag] = VolumeLevel;
            tag[WindowPosXTag] = WindowPositionX;
            tag[WindowPosYTag] = WindowPositionY;
            tag[PanelOpacityTag] = PanelOpacity;
            tag[BlurLevelTag] = BlurLevel;
            tag[CornerRadiusTag] = CornerRadius;
            tag[PanelColorRTag] = PanelColor.R;
            tag[PanelColorGTag] = PanelColor.G;
            tag[PanelColorBTag] = PanelColor.B;
            tag[SecondaryColorRTag] = SecondaryColor.R;
            tag[SecondaryColorGTag] = SecondaryColor.G;
            tag[SecondaryColorBTag] = SecondaryColor.B;
            tag[PanelBgRTag] = PanelBackgroundColor.R;
            tag[PanelBgGTag] = PanelBackgroundColor.G;
            tag[PanelBgBTag] = PanelBackgroundColor.B;
            tag[MiniPlayerEnabledTag] = MiniPlayerEnabled;
            tag[VolumeOverrideEnabledTag] = VolumeOverrideEnabled;
            tag[SoundVolumeLevelTag] = SoundVolumeLevel;
            tag[AmbientVolumeLevelTag] = AmbientVolumeLevel;
            tag[OriginalSoundVolumeTag] = OriginalSoundVolume;
            tag[OriginalAmbientVolumeTag] = OriginalAmbientVolume;
            // BossMusicUuid / DeathMusicUuid not saved — session-only
        }
    }
}
