using Microsoft.Xna.Framework;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Content.UI.TerraUI.Enums;
using Terra_Namp.Content.UI.SoundpadUI;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.UI;
using Terra_Namp.Networking;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp.Common.Players
{
    public class TerraModPlayer : ModPlayer
    {
        private const int HoldDelay = 30;     // 0.5s before repeat starts
        private const int RepeatInterval = 3; // 0.05s between repeats
        private const float InitialStep = 0.05f; // 5% on first press
        private const float RepeatStep = 0.01f;  // 1% per repeat tick

        private int volumeUpHoldTicks;
        private int volumeDownHoldTicks;

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Terra_Namp.MusicPlayerBind.JustPressed)
            {
                var state = TerraUILoader.GetUIState<TerraState>();
                var prevMode = state.ViewMode;
                state.CycleViewMode();

                if (state.ViewMode == PlayerViewMode.Hidden)
                    SoundEngine.PlaySound(SoundID.MenuClose);
                else if (prevMode == PlayerViewMode.Hidden)
                    SoundEngine.PlaySound(SoundID.MenuOpen);
            }

            if (Terra_Namp.SoundpadBind.JustPressed)
            {
                var soundpadState = TerraUILoader.GetUIState<SoundpadState>();
                soundpadState.Visible = !soundpadState.Visible;

                SoundEngine.PlaySound(soundpadState.Visible ? SoundID.MenuOpen : SoundID.MenuClose);
            }

            // Volume Up (hold-aware)
            if (Terra_Namp.VolumeUpBind.Current)
            {
                if (Terra_Namp.VolumeUpBind.JustPressed)
                {
                    AdjustPlayerVolume(InitialStep);
                    volumeUpHoldTicks = 0;
                }
                else
                {
                    volumeUpHoldTicks++;
                    if (volumeUpHoldTicks >= HoldDelay && (volumeUpHoldTicks - HoldDelay) % RepeatInterval == 0)
                        AdjustPlayerVolume(RepeatStep);
                }
                VolumeHudState.Show(PersistentDataStoreSystem.GetDataStore<TerraDataStore>().VolumeLevel);
            }
            else
            {
                if (volumeUpHoldTicks > 0)
                    PersistentDataStoreSystem.GetDataStore<TerraDataStore>().ForceSave();
                volumeUpHoldTicks = 0;
            }

            // Volume Down (hold-aware)
            if (Terra_Namp.VolumeDownBind.Current)
            {
                if (Terra_Namp.VolumeDownBind.JustPressed)
                {
                    AdjustPlayerVolume(-InitialStep);
                    volumeDownHoldTicks = 0;
                }
                else
                {
                    volumeDownHoldTicks++;
                    if (volumeDownHoldTicks >= HoldDelay && (volumeDownHoldTicks - HoldDelay) % RepeatInterval == 0)
                        AdjustPlayerVolume(-RepeatStep);
                }
                VolumeHudState.Show(PersistentDataStoreSystem.GetDataStore<TerraDataStore>().VolumeLevel);
            }
            else
            {
                if (volumeDownHoldTicks > 0)
                    PersistentDataStoreSystem.GetDataStore<TerraDataStore>().ForceSave();
                volumeDownHoldTicks = 0;
            }
        }

        private void AdjustPlayerVolume(float delta)
        {
            var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            float newVolume = MathHelper.Clamp(store.VolumeLevel + delta, 0f, 1f);

            if (newVolume == store.VolumeLevel)
                return;

            store.VolumeLevel = newVolume;

            // Save immediately on initial press, debounce on hold
            if (Terra_Namp.VolumeUpBind.JustPressed || Terra_Namp.VolumeDownBind.JustPressed)
                store.ForceSave();
        }

        public override void OnEnterWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetLogger.State($"OnEnterWorld: requesting jukebox state from server (player {Main.myPlayer})");
                // Request the current jukebox state from the server.
                var packet = PacketBuilder.RequestState((byte)Main.myPlayer);
                packet.Send();
            }
        }
    }
}
