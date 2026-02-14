using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class PermissionSyncHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte superCount = reader.ReadByte();
        var superUsers = new HashSet<int>();
        for (int i = 0; i < superCount; i++)
            superUsers.Add(reader.ReadByte());

        byte count = reader.ReadByte();
        var permissions = new Dictionary<int, PlayerPermissions>();

        for (int i = 0; i < count; i++)
        {
            byte playerIndex = reader.ReadByte();
            var role = (PermissionRole)reader.ReadByte();

            if (playerIndex < Main.maxPlayers)
                permissions[playerIndex] = new PlayerPermissions { Role = role };
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Packet($"PermissionSync received: {count} players, {superCount} super users");
            ClientPermissionCache.Update(permissions, superUsers);
        }
    }
}
