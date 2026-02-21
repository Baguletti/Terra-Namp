using System.IO;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SetDeathSoundpadHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();
        string uuid = PacketBuilder.ReadString(reader);

        if (Main.netMode == NetmodeID.Server)
        {
            var state = ServerJukeboxState.Instance;
            if (!state.GetPermissions(whoAmI).CanManage)
            {
                NetLogger.Packet($"SetDeathSoundpad DENIED for player {whoAmI} (no manage permission)");
                return;
            }

            NetLogger.State($"[SetDeathSoundpad] player {whoAmI} set death soundpad uuid={uuid[..8]}..");
            state.DeathSoundpadUuid = uuid;
            state.DeathMusicHash = null; // mutually exclusive
            state.DeathMusicTitle = "";
            state.DeathMusicAuthor = "";

            PacketBuilder.SetDeathSoundpad(sender, uuid).Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.State($"[SetDeathSoundpad] received: uuid={uuid[..8]}..");
            var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
            var tStore = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            store.DeathSoundUuid = uuid;
            tStore.DeathMusicUuid = ""; // mutually exclusive
            store.ForceSave();
            tStore.ForceSave();
        }
    }
}
