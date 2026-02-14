using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class RequestSongHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte requester = reader.ReadByte();
        byte[] hash = reader.ReadBytes(16);
        string hashHex = ContentHash.HashToHex(hash);

        if (Main.netMode == NetmodeID.Server)
        {
            var state = ServerJukeboxState.Instance;
            var transferMgr = SongTransferManager.Instance;

            string serverPath = SongRegistry.Instance.GetServerCachePath(hashHex);
            if (File.Exists(serverPath))
            {
                NetLogger.Transfer($"RequestSong from client {whoAmI}: hash={hashHex[..8]}.. -> SERVING FROM SERVER CACHE");
                transferMgr.ServeFromServerCache(hashHex, whoAmI);
                return;
            }

            if (transferMgr.HasServerTransfer(hashHex))
            {
                NetLogger.Transfer($"RequestSong from client {whoAmI}: hash={hashHex[..8]}.. -> added to existing transfer waiting list");
                transferMgr.AddWaitingClient(hashHex, whoAmI);
                return;
            }

            int sourceClient = state.DjPlayerIndex;
            if (sourceClient < 0 || sourceClient == whoAmI
                || sourceClient >= Main.maxPlayers || !Main.player[sourceClient].active)
            {
                NetLogger.Error($"RequestSong from client {whoAmI}: hash={hashHex[..8]}.. -> no valid source client (DJ={sourceClient})");
                return;
            }

            NetLogger.Transfer($"RequestSong from client {whoAmI}: hash={hashHex[..8]}.. -> forwarding to source client {sourceClient}");
            transferMgr.CreateServerTransfer(hashHex, sourceClient, new List<int> { whoAmI });

            var packet = PacketBuilder.RequestSong(requester, hash);
            packet.Send(sourceClient);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            string localUuid = SongRegistry.Instance.GetUuidByHash(hashHex);
            if (localUuid == null)
            {
                NetLogger.Error($"RequestSong received but hash={hashHex[..8]}.. NOT found locally!");
                return;
            }

            string filePath = Path.Combine(Terra_Namp.CachePath, $"{localUuid}.mp3");
            if (!File.Exists(filePath))
            {
                NetLogger.Error($"RequestSong: file not found for uuid={localUuid[..8]}.. path={filePath}");
                return;
            }

            long fileSize = new FileInfo(filePath).Length;
            NetLogger.Transfer($"RequestSong: starting upload for hash={hashHex[..8]}.. size={fileSize / 1024}KB");
            SongTransferManager.Instance.BeginOutboundTransfer(hashHex, filePath);
        }
    }
}
