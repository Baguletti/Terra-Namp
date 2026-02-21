using System.IO;
using Terra_Namp.Content.IO;
using Terra_Namp.Core.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SetBossSoundpadHandler
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
                NetLogger.Packet($"SetBossSoundpad DENIED for player {whoAmI} (no manage permission)");
                return;
            }

            NetLogger.State($"[SetBossSoundpad] player {whoAmI} set boss soundpad uuid={uuid[..8]}..");
            state.BossSoundpadUuid = uuid;
            state.BossMusicHash = null; // mutually exclusive
            state.BossMusicTitle = "";
            state.BossMusicAuthor = "";

            PacketBuilder.SetBossSoundpad(sender, uuid).Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.State($"[SetBossSoundpad] received: uuid={uuid[..8]}..");
            var store = PersistentDataStoreSystem.GetDataStore<SoundpadDataStore>();
            var tStore = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            store.BossSoundUuid = uuid;
            tStore.BossMusicUuid = ""; // mutually exclusive
            store.ForceSave();
            tStore.ForceSave();
        }
    }
}
