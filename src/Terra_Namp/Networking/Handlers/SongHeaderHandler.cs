using System.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SongHeaderHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte[] hash = reader.ReadBytes(16);
        int totalSize = reader.ReadInt32();
        string title = PacketBuilder.ReadString(reader);
        string author = PacketBuilder.ReadString(reader);
        string hashHex = ContentHash.HashToHex(hash);

        if (Main.netMode == NetmodeID.Server)
        {
            NetLogger.Transfer($"SongHeader from client {whoAmI}: \"{title}\" size={totalSize / 1024}KB hash={hashHex[..8]}..");
            SongTransferManager.Instance.OnServerSongHeaderReceived(hashHex, totalSize, title, author);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Transfer($"SongHeader received: \"{title}\" by {author} size={totalSize / 1024}KB hash={hashHex[..8]}..");
            SongTransferManager.Instance.OnSongHeaderReceived(hashHex, totalSize, title, author);
        }
    }
}
