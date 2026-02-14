using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp.Networking.Handlers;

public static class PrefetchListHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();
        ushort count = reader.ReadUInt16();

        var songs = new System.Collections.Generic.List<(byte[] Hash, string HashHex, string Title, string Author)>();
        for (int i = 0; i < count; i++)
        {
            byte[] hash = reader.ReadBytes(16);
            string title = PacketBuilder.ReadString(reader);
            string author = PacketBuilder.ReadString(reader);
            songs.Add((hash, ContentHash.HashToHex(hash), title, author));
        }

        if (Main.netMode == NetmodeID.Server)
        {
            NetLogger.Transfer($"PrefetchList from client {whoAmI}: {count} songs -> broadcasting to all clients");

            // Rebuild and broadcast to all clients except sender.
            var hashList = new System.Collections.Generic.List<(byte[] Hash, string Title, string Author)>();
            foreach (var (hash, _, title, author) in songs)
                hashList.Add((hash, title, author));

            var packet = PacketBuilder.PrefetchList(sender, hashList);
            packet.Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (!ModContent.GetInstance<Terra_NampConfig>().EnablePrefetch)
            {
                NetLogger.Transfer($"PrefetchList received ({count} songs) but prefetch is disabled in config");
                return;
            }

            var transferMgr = SongTransferManager.Instance;
            int queued = 0;

            foreach (var (hash, hashHex, title, author) in songs)
            {
                if (SongRegistry.Instance.HasHash(hashHex))
                    continue;

                if (transferMgr.QueuePrefetch(hashHex, hash))
                    queued++;
            }

            NetLogger.Transfer($"PrefetchList: {count} songs received, {queued} queued for prefetch");
        }
    }
}
