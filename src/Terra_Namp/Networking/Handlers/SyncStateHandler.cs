using System.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SyncStateHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        bool isPlaying = reader.ReadBoolean();
        bool isPaused = reader.ReadBoolean();
        byte[] hash = reader.ReadBytes(16);
        float progress = reader.ReadSingle();
        string title = PacketBuilder.ReadString(reader);
        string author = PacketBuilder.ReadString(reader);
        bool forced = reader.ReadBoolean();
        bool slowedReverb = reader.ReadBoolean();

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        string hashHex = ContentHash.HashToHex(hash);
        NetLogger.State($"SyncState received: playing={isPlaying} paused={isPaused} progress={progress:F3} forced={forced} title=\"{title}\" hash={hashHex[..8]}..");

        if (!isPlaying)
        {
            NetLogger.State("SyncState: nothing playing on server");
            return;
        }

        bool allZero = true;
        for (int i = 0; i < hash.Length; i++)
        {
            if (hash[i] != 0) { allZero = false; break; }
        }
        if (allZero)
        {
            NetLogger.State("SyncState: hash is all zeros, ignoring");
            return;
        }

        string localUuid = SongRegistry.Instance.GetUuidByHash(hashHex);

        if (localUuid != null)
        {
            NetLogger.State($"SyncState: song found locally uuid={localUuid[..8]}.. seeking to {progress:F3}");
            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                panel?.BeginPlayingSongFromNetwork(localUuid, forced, progress);

                if (isPaused)
                    panel?.ActiveSong?.PauseFromNetwork();

                if (panel != null)
                    panel.SlowedReverbActive = slowedReverb;
                if (slowedReverb)
                    panel?.ActiveSong?.ApplySlowedReverbFromNetwork(true);
            });
        }
        else
        {
            NetLogger.Transfer($"SyncState: song NOT in cache, requesting transfer for hash={hashHex[..8]}..");
            SongTransferManager.Instance?.SetPendingPlayback(hashHex, title, author, forced, progress, slowedReverb);

            var requestPacket = PacketBuilder.RequestSong((byte)Main.myPlayer, hash);
            requestPacket.Send();
        }
    }
}
