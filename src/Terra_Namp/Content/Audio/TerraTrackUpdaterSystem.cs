using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.IO;
using Terra_Namp.Core.UI;
using Terra_Namp.Networking;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp.Content.Audio
{
    public class TerraTrackUpdaterSystem : ModSystem
    {
        public bool CurrentlyForcingSong { get; set; }

        public bool CurrentlyFadingOut { get; private set; }

        public override void PostUpdateInput()
        {
            if (Main.gameMenu || Main.dedServ)
                return;

            CurrentlyForcingSong = false;

            TerraMainPanel panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
            if (panel == null) return;

            HandleFade(panel);
            panel.UpdateActiveSong();
            ApplyVolumeOverride();
        }

        private void HandleFade(TerraMainPanel panel)
        {
            int silentSlot = MusicLoader.GetMusicSlot(Mod, Terra_Namp.Silence);

            if (panel.ActiveSong != null && CurrentlyForcingSong)
            {
                panel.ActiveSong.VolumeFadeMultiplier = Main.musicFade[silentSlot];
            }

            if (!CurrentlyForcingSong && panel.ActiveSong != null && panel.ActiveSong.Forced)
            {
                CurrentlyFadingOut = true;
            }

            if (panel.ActiveSong != null && CurrentlyFadingOut)
            {
                panel.ActiveSong.VolumeFadeMultiplier -= 1 / 180f;

                if (panel.ActiveSong.VolumeFadeMultiplier <= 0)
                {
                    panel.StopCurrentSong();
                    CurrentlyFadingOut = false;
                }
            }

            if (panel.ActiveSong == null)
            {
                CurrentlyForcingSong = CurrentlyFadingOut = false;
            }
        }

        private static void ApplyVolumeOverride()
        {
            var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();

            if (!store.VolumeOverrideEnabled)
            {
                // Restore original values when override is disabled
                if (store.OriginalSoundVolume >= 0f)
                {
                    Main.soundVolume = store.OriginalSoundVolume;
                    store.OriginalSoundVolume = -1f;
                }
                if (store.OriginalAmbientVolume >= 0f)
                {
                    Main.ambientVolume = store.OriginalAmbientVolume;
                    store.OriginalAmbientVolume = -1f;
                }
                return;
            }

            // Save original values ONLY ONCE when override is first enabled
            // Persist to disk so they survive recompile/reload
            if (store.OriginalSoundVolume < 0f)
            {
                store.OriginalSoundVolume = Main.soundVolume;
                store.ForceSave();
            }

            if (store.OriginalAmbientVolume < 0f)
            {
                store.OriginalAmbientVolume = Main.ambientVolume;
                store.ForceSave();
            }

            // Apply cubic curve to sound volume using saved original
            float s = store.SoundVolumeLevel;
            Main.soundVolume = store.OriginalSoundVolume * s * s * s;

            // Apply cubic curve to ambient volume
            // NOTE: Vanilla Terraria ambient sounds (wind, birds) do NOT respect Main.ambientVolume dynamically.
            // This only works if the player has mods like TerrariaAmbience installed, which create custom
            // ambient sounds that properly read Main.ambientVolume every frame.
            float a = store.AmbientVolumeLevel;
            Main.ambientVolume = store.OriginalAmbientVolume * a * a * a;
        }


        public override void PreSaveAndQuit()
        {
            Core.IO.PersistentDataStoreSystem.GetDataStore<Content.IO.TerraDataStore>().ForceSave();
        }

        /// <summary>
        /// Server-side: detect DJ disconnect and stop playback for all clients.
        /// Runs every tick on the server (PostUpdateWorld runs on all sides, unlike PostUpdateInput).
        /// </summary>
        public override void PostUpdateWorld()
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            var jukeboxState = ServerJukeboxState.Instance;
            if (jukeboxState == null || jukeboxState.DjPlayerIndex < 0 || !jukeboxState.IsPlaying)
                return;

            if (!Main.player[jukeboxState.DjPlayerIndex].active)
            {
                NetLogger.State($"DJ player {jukeboxState.DjPlayerIndex} disconnected — stopping playback");
                jukeboxState.StopPlayback();
                // Broadcast stop to all remaining clients
                PacketBuilder.StopSong(255).Send();
            }
        }

        public override void Unload()
        {
            if (!Main.dedServ)
            {
                // Dispose active song to prevent orphaned audio handles
                TerraMainPanel panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                if (panel?.ActiveSong != null)
                {
                    panel.ActiveSong.Dispose();
                    panel.ActiveSong = null;
                }

                // Сохранить настройки при выгрузке мода (включая перекомпиляцию в dev mode)
                Core.IO.PersistentDataStoreSystem.GetDataStore<Content.IO.TerraDataStore>()?.ForceSave();
            }
        }
    }
}
