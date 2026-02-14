using System.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SongTransferCompleteHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte[] hash = reader.ReadBytes(16);
        string hashHex = ContentHash.HashToHex(hash);

        if (Main.netMode == NetmodeID.Server)
        {
            NetLogger.Transfer($"SongTransferComplete from client {whoAmI}: hash={hashHex[..8]}.. -> caching on server");
            SongTransferManager.Instance.OnServerSongTransferComplete(hashHex);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Transfer($"SongTransferComplete received: hash={hashHex[..8]}.. -> verifying and saving");
            SongTransferManager.Instance.OnSongTransferComplete(hashHex);
        }
    }
}
