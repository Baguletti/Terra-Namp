using System.IO;
using Terra_Namp.Networking.Handlers;
using Terraria;

namespace Terra_Namp.Networking;

public static class PacketRouter
{
    public static void HandlePacket(BinaryReader reader, int whoAmI)
    {
        PacketType type = (PacketType)reader.ReadByte();

        string side = Main.netMode == Terraria.ID.NetmodeID.Server ? "SRV" : "CLI";
        if (type != PacketType.SongChunk) // Don't spam chunk logs
            NetLogger.Packet($"[{side}] Received {type} from={whoAmI}");

        switch (type)
        {
            case PacketType.PlaySong:
                PlaySongHandler.Handle(reader, whoAmI);
                break;
            case PacketType.StopSong:
                StopSongHandler.Handle(reader, whoAmI);
                break;
            case PacketType.PauseSong:
                PauseSongHandler.Handle(reader, whoAmI);
                break;
            case PacketType.ResumeSong:
                ResumeSongHandler.Handle(reader, whoAmI);
                break;
            case PacketType.SeekPosition:
                SeekPositionHandler.Handle(reader, whoAmI);
                break;
            case PacketType.SlowedReverb:
                SlowedReverbHandler.Handle(reader, whoAmI);
                break;
            case PacketType.RequestSong:
                RequestSongHandler.Handle(reader, whoAmI);
                break;
            case PacketType.SongHeader:
                SongHeaderHandler.Handle(reader, whoAmI);
                break;
            case PacketType.SongChunk:
                SongChunkHandler.Handle(reader, whoAmI);
                break;
            case PacketType.SongTransferComplete:
                SongTransferCompleteHandler.Handle(reader, whoAmI);
                break;
            case PacketType.SyncState:
                SyncStateHandler.Handle(reader, whoAmI);
                break;
            case PacketType.RequestState:
                RequestStateHandler.Handle(reader, whoAmI);
                break;
            case PacketType.PrefetchList:
                PrefetchListHandler.Handle(reader, whoAmI);
                break;
            case PacketType.PermissionUpdate:
                PermissionUpdateHandler.Handle(reader, whoAmI);
                break;
            case PacketType.PermissionSync:
                PermissionSyncHandler.Handle(reader, whoAmI);
                break;
        }
    }
}
