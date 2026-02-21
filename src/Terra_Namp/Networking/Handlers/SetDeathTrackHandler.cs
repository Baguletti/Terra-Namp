using System.IO;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SetDeathTrackHandler
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
                NetLogger.Packet($"SetDeathTrack DENIED for player {whoAmI} (no manage permission)");
                return;
            }

            NetLogger.State($"[SetDeathTrack] player {whoAmI} set death track=\"{title}\" hash={hashHex[..8]}..");
            state.DeathMusicHash = hash;
            state.DeathMusicTitle = title;
            state.DeathMusicAuthor = author;
            state.DeathSoundpadUuid = ""; // mutually exclusive

            PacketBuilder.SetDeathTrack(sender, hash, title, author).Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.State($"[SetDeathTrack] received: \"{title}\" hash={hashHex[..8]}..");
            var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            var sStore = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
            string uuid = SongRegistry.Instance.GetUuidByHash(hashHex);
            if (uuid != null)
            {
                store.DeathMusicUuid = uuid;
                sStore.DeathSoundUuid = ""; // mutually exclusive
                store.ForceSave();
                sStore.ForceSave();
            }
        }
    }
}
