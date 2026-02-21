using System.IO;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SetBossTrackHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();
        byte[] hash = reader.ReadBytes(16);
        string title = PacketBuilder.ReadString(reader);
        string author = PacketBuilder.ReadString(reader);
        string hashHex = ContentHash.HashToHex(hash);

        if (Main.netMode == NetmodeID.Server)
        {
            var state = ServerJukeboxState.Instance;
            if (!state.GetPermissions(whoAmI).CanManage)
            {
                NetLogger.Packet($"SetBossTrack DENIED for player {whoAmI} (no manage permission)");
                return;
            }

            NetLogger.State($"[SetBossTrack] player {whoAmI} set boss track=\"{title}\" hash={hashHex[..8]}..");
            state.BossMusicHash = hash;
            state.BossMusicTitle = title;
            state.BossMusicAuthor = author;
            state.BossSoundpadUuid = ""; // mutually exclusive

            PacketBuilder.SetBossTrack(sender, hash, title, author).Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.State($"[SetBossTrack] received: \"{title}\" hash={hashHex[..8]}..");
            var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            var sStore = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
            string uuid = SongRegistry.Instance.GetUuidByHash(hashHex);
            if (uuid != null)
            {
                store.BossMusicUuid = uuid;
                sStore.BossSoundUuid = ""; // mutually exclusive
                store.ForceSave();
                sStore.ForceSave();
            }
        }
    }
}
