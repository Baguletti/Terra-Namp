using System.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SongChunkHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte[] hash = reader.ReadBytes(16);
        int chunkIndex = reader.ReadInt32();
        ushort chunkSize = reader.ReadUInt16();
        byte[] data = reader.ReadBytes(chunkSize);
        string hashHex = ContentHash.HashToHex(hash);

        // Log every 50th chunk to avoid spam, plus first and last.
        if (chunkIndex % 50 == 0)
        {
            string side = Main.netMode == NetmodeID.Server ? "SRV" : "CLI";
            NetLogger.Transfer($"[{side}] SongChunk #{chunkIndex} ({chunkSize}B) for hash={hashHex[..8]}.. from={whoAmI}");
        }

        if (Main.netMode == NetmodeID.Server)
        {
            SongTransferManager.Instance.OnServerSongChunkReceived(hashHex, chunkIndex, data, chunkSize);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            SongTransferManager.Instance.OnSongChunkReceived(hashHex, chunkIndex, data, chunkSize);
        }
    }
}
