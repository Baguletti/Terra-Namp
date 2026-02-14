using Terraria;
using Terraria.ModLoader;

namespace Terra_Namp.Networking;

public class SuperUserCommand : ModCommand
{
    public override string Command => "terra-namp-superuser";
    public override CommandType Type => CommandType.Console;
    public override string Usage => "/terra-namp-superuser <player_name>";
    public override string Description => "Grant Terra Namp super user status (immutable admin) to a player";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 1)
        {
            caller.Reply("Usage: terra-namp-superuser <player_name>");
            return;
        }

        string targetName = string.Join(" ", args);
        int targetIndex = AdminCommand.FindPlayerByName(targetName);

        if (targetIndex < 0)
        {
            caller.Reply($"Player \"{targetName}\" not found.");
            return;
        }

        var state = ServerJukeboxState.Instance;
        state.EnsurePlayerRegistered(targetIndex);

        // Set to Admin + super user
        state.Permissions[targetIndex] = new PlayerPermissions { Role = PermissionRole.Admin };
        state.SuperUsers.Add(targetIndex);

        caller.Reply($"Player \"{Main.player[targetIndex].name}\" (index {targetIndex}) is now a Terra Namp super user.");

        var syncPacket = PacketBuilder.PermissionSync(state.Permissions, state.SuperUsers);
        syncPacket.Send();
    }
}
