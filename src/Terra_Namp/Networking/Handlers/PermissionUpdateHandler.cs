using System.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class PermissionUpdateHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();
        byte targetPlayer = reader.ReadByte();
        var role = (PermissionRole)reader.ReadByte();

        if (Main.netMode != NetmodeID.Server)
            return;

        var state = ServerJukeboxState.Instance;

        if (!state.GetPermissions(whoAmI).CanManage)
        {
            NetLogger.Packet($"PermissionUpdate DENIED for player {whoAmI} (not admin)");
            return;
        }

        // Validate target player index
        if (targetPlayer >= Main.maxPlayers || !Main.player[targetPlayer].active)
        {
            NetLogger.Packet($"PermissionUpdate DENIED: player {targetPlayer} is not active");
            return;
        }

        // Prevent admin from modifying their own permissions
        if (targetPlayer == whoAmI)
        {
            NetLogger.Packet($"PermissionUpdate DENIED: player {whoAmI} cannot modify own permissions");
            return;
        }

        // Super users cannot be modified by anyone
        if (state.IsSuperUser(targetPlayer))
        {
            NetLogger.Packet($"PermissionUpdate DENIED: player {targetPlayer} is super user");
            return;
        }

        state.Permissions[targetPlayer] = new PlayerPermissions { Role = role };

        NetLogger.Packet($"PermissionUpdate: player {targetPlayer} -> role={role}");

        // Broadcast updated permissions to all clients
        var syncPacket = PacketBuilder.PermissionSync(state.Permissions, state.SuperUsers);
        syncPacket.Send();
    }
}
