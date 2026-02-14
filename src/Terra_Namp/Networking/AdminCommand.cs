using Terraria;
using Terraria.ModLoader;

namespace Terra_Namp.Networking;

public class AdminCommand : ModCommand
{
    public override string Command => "terra-namp-admin";
    public override CommandType Type => CommandType.Console;
    public override string Usage => "/terra-namp-admin <player_name>";
    public override string Description => "Grant Terra Namp admin permissions to a player";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 1)
        {
            caller.Reply("Usage: terra-namp-admin <player_name>");
            return;
        }

        string targetName = string.Join(" ", args);
        int targetIndex = FindPlayerByName(targetName);

        if (targetIndex < 0)
        {
            caller.Reply($"Player \"{targetName}\" not found.");
            return;
        }

        var state = ServerJukeboxState.Instance;
        state.EnsurePlayerRegistered(targetIndex);
        state.Permissions[targetIndex] = new PlayerPermissions { Role = PermissionRole.Admin };

        // First admin on dedicated server = super user
        if (state.SuperUsers.Count == 0)
            state.SuperUsers.Add(targetIndex);

        bool isSuperUser = state.IsSuperUser(targetIndex);
        caller.Reply($"Player \"{Main.player[targetIndex].name}\" (index {targetIndex}) is now a Terra Namp admin{(isSuperUser ? " (super user)" : "")}.");

        var syncPacket = PacketBuilder.PermissionSync(state.Permissions, state.SuperUsers);
        syncPacket.Send();
    }

    internal static int FindPlayerByName(string name)
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (Main.player[i] != null && Main.player[i].active
                && Main.player[i].name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
}
