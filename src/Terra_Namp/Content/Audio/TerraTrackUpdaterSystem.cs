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

        private readonly JukeboxEventMachine _serverEventMachine = new(Main.maxPlayers);
        private readonly JukeboxEventMachine _spEventMachine = new(Main.maxPlayers);
        private int diagTick; // throttle diagnostic logs

        public override void PostUpdateInput()
        {
            if (Main.gameMenu || Main.dedServ)
                return;

            CurrentlyForcingSong = false;

            TerraMainPanel panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;

            // Throttled diagnostics every 5 seconds
            diagTick++;
            if (diagTick >= 300)
            {
                diagTick = 0;
                NetLogger.State($"[DIAG] panel={panel != null} netMode={Main.netMode} " +
                    $"SinglePlayer={NetmodeID.SinglePlayer} dead={Main.LocalPlayer?.dead} " +
                    $"BossMusicUuid='{PersistentDataStoreSystem.GetDataStore<TerraDataStore>().BossMusicUuid}' " +
                    $"DeathMusicUuid='{PersistentDataStoreSystem.GetDataStore<TerraDataStore>().DeathMusicUuid}'");
            }

            if (panel == null) return;

            HandleFade(panel);
            panel.UpdateActiveSong();
            ApplyVolumeOverride();

            // Boss/death music — singleplayer only (multiplayer handled server-side in PostUpdateWorld)
            if (Main.netMode == NetmodeID.SinglePlayer)
                HandleSinglePlayerEvents(panel);
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

        private void HandleSinglePlayerEvents(TerraMainPanel panel)
        {
            bool anyBoss = false;
            for (int i = 0; i < Main.maxNPCs; i++)
                if (Main.npc[i].active && Main.npc[i].boss) { anyBoss = true; break; }

            bool[] playerDead = new bool[Main.maxPlayers];
            for (int i = 0; i < Main.maxPlayers; i++)
                playerDead[i] = Main.player[i].active && Main.player[i].dead;

            var tStore = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            var sStore = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();

            // Build config from local DataStores (SP uses UUID directly, no hash lookup needed)
            // BossTrackHash/DeathTrackHash are faked as non-null when UUID is set so the machine
            // fires the right event type; actual playback uses BeginPlayingSongLocalOnly(uuid).
            byte[] bossHash  = string.IsNullOrEmpty(tStore.BossMusicUuid)  ? null : new byte[1];
            byte[] deathHash = string.IsNullOrEmpty(tStore.DeathMusicUuid) ? null : new byte[1];

            var config = new JukeboxEventMachine.JukeboxConfig(
                bossHash,
                string.IsNullOrEmpty(sStore.BossSoundUuid)  ? null : sStore.BossSoundUuid,
                deathHash,
                string.IsNullOrEmpty(sStore.DeathSoundUuid) ? null : sStore.DeathSoundUuid);

            var events = _spEventMachine.Tick(
                new JukeboxEventMachine.GameState(playerDead, anyBoss), config);

            foreach (var ev in events)
            {
                switch (ev.Type)
                {
                    case JukeboxEventMachine.EventType.PlayBossTrack:
                        NetLogger.State($"[SP/BossMusic] Boss spawned, playing uuid={tStore.BossMusicUuid[..8]}..");
                        if (panel.ActiveSong?.Uuid != tStore.BossMusicUuid)
                            panel.BeginPlayingEventSong(tStore.BossMusicUuid, forced: false);
                        break;

                    case JukeboxEventMachine.EventType.PlayBossSoundpad:
                        NetLogger.State($"[SP/BossMusic] Playing soundpad boss sound uuid={sStore.BossSoundUuid[..8]}..");
                        panel.SoundpadPlayback?.PlaySound(sStore.BossSoundUuid);
                        break;

                    case JukeboxEventMachine.EventType.StopBossTrack:
                        NetLogger.State("[SP/BossMusic] Boss died — stopping boss track");
                        panel.RestorePreEventState();
                        break;

                    case JukeboxEventMachine.EventType.PlayDeathTrack:
                        NetLogger.State($"[SP/DeathMusic] Player died, playing uuid={tStore.DeathMusicUuid[..8]}..");
                        panel.BeginPlayingEventSong(tStore.DeathMusicUuid, forced: false);
                        break;

                    case JukeboxEventMachine.EventType.PlayDeathSoundpad:
                        NetLogger.State($"[SP/DeathMusic] Playing soundpad death sound uuid={sStore.DeathSoundUuid[..8]}..");
                        panel.SoundpadPlayback?.PlaySound(sStore.DeathSoundUuid);
                        break;

                    case JukeboxEventMachine.EventType.StopDeathTrack:
                        NetLogger.State("[SP/DeathMusic] Timer expired — stopping death track");
                        panel.RestorePreEventState();
                        break;
                }
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
        /// Server-side: DJ disconnect, boss/death events for all clients.
        /// PostUpdateWorld runs on all sides; early-exit on non-server.
        /// </summary>
        public override void PostUpdateWorld()
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            var jukeboxState = ServerJukeboxState.Instance;
            if (jukeboxState == null) return;

            // DJ disconnect
            if (jukeboxState.DjPlayerIndex >= 0 && jukeboxState.IsPlaying &&
                !Main.player[jukeboxState.DjPlayerIndex].active)
            {
                NetLogger.State($"DJ player {jukeboxState.DjPlayerIndex} disconnected — stopping playback");
                jukeboxState.StopPlayback();
                PacketBuilder.StopSong(255).Send();
            }

            // Boss/death events via pure state machine (see JukeboxEventMachine.cs)
            bool anyBoss = false;
            for (int i = 0; i < Main.maxNPCs; i++)
                if (Main.npc[i].active && Main.npc[i].boss) { anyBoss = true; break; }

            bool[] playerDead = new bool[Main.maxPlayers];
            for (int i = 0; i < Main.maxPlayers; i++)
                playerDead[i] = Main.player[i].active && Main.player[i].dead;

            var config = new JukeboxEventMachine.JukeboxConfig(
                jukeboxState.BossMusicHash,
                jukeboxState.BossSoundpadUuid,
                jukeboxState.DeathMusicHash,
                jukeboxState.DeathSoundpadUuid);

            var events = _serverEventMachine.Tick(
                new JukeboxEventMachine.GameState(playerDead, anyBoss), config);

            foreach (var ev in events)
            {
                switch (ev.Type)
                {
                    case JukeboxEventMachine.EventType.PlayBossTrack:
                        NetLogger.State($"[BossEvent] Broadcasting boss track \"{jukeboxState.BossMusicTitle}\"");
                        jukeboxState.StartPlayback(jukeboxState.BossMusicHash, jukeboxState.BossMusicTitle, jukeboxState.BossMusicAuthor, -1, false);
                        PacketBuilder.PlaySong(255, jukeboxState.BossMusicHash, jukeboxState.BossMusicTitle, jukeboxState.BossMusicAuthor, false).Send();
                        break;

                    case JukeboxEventMachine.EventType.PlayBossSoundpad:
                        NetLogger.State($"[BossEvent] Broadcasting boss soundpad uuid={jukeboxState.BossSoundpadUuid[..8]}..");
                        PacketBuilder.PlaySoundpadSound(jukeboxState.BossSoundpadUuid).Send();
                        break;

                    case JukeboxEventMachine.EventType.StopBossTrack:
                        NetLogger.State("[BossEvent] Boss died — stopping boss music");
                        jukeboxState.StopPlayback();
                        PacketBuilder.StopSong(255).Send();
                        break;

                    case JukeboxEventMachine.EventType.PlayDeathTrack:
                        NetLogger.State($"[DeathEvent] Broadcasting death track \"{jukeboxState.DeathMusicTitle}\"");
                        jukeboxState.StartPlayback(jukeboxState.DeathMusicHash, jukeboxState.DeathMusicTitle, jukeboxState.DeathMusicAuthor, -1, false);
                        PacketBuilder.PlaySong(255, jukeboxState.DeathMusicHash, jukeboxState.DeathMusicTitle, jukeboxState.DeathMusicAuthor, false).Send();
                        break;

                    case JukeboxEventMachine.EventType.PlayDeathSoundpad:
                        NetLogger.State($"[DeathEvent] Broadcasting death soundpad uuid={jukeboxState.DeathSoundpadUuid[..8]}..");
                        PacketBuilder.PlaySoundpadSound(jukeboxState.DeathSoundpadUuid).Send();
                        break;

                    case JukeboxEventMachine.EventType.StopDeathTrack:
                        NetLogger.State("[DeathEvent] Timer expired — stopping death music");
                        jukeboxState.StopPlayback();
                        PacketBuilder.StopSong(255).Send();
                        break;
                }
            }

            // Keep ServerJukeboxState timer in sync for packet handlers that query it
            jukeboxState.DeathMusicTimer = _serverEventMachine.DeathMusicTimer;
            jukeboxState.WasAnyBossAlive = anyBoss;
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
