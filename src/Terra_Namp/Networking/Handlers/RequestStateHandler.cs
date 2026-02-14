using System.IO;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class RequestStateHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte requester = reader.ReadByte();

        if (Main.netMode != NetmodeID.Server)
            return;

        var state = ServerJukeboxState.Instance;

        // Register player with default permissions (host auto-detected as Admin)
        state.EnsurePlayerRegistered(whoAmI);

        NetLogger.State($"RequestState from player {whoAmI}: isPlaying={state.IsPlaying} isPaused={state.IsPaused} title=\"{state.Title}\"");

        byte[] hash = state.CurrentSongHash ?? new byte[16];
        float progress = state.LastKnownProgress;

        var packet = PacketBuilder.SyncState(
            state.IsPlaying,
            state.IsPaused,
            hash,
            progress,
            state.Title ?? "",
            state.Author ?? "",
            state.IsForced,
            state.SlowedReverbEnabled
        );

        packet.Send(whoAmI);
        NetLogger.State($"SyncState sent to player {whoAmI}");

        // Send permission state
        var permPacket = PacketBuilder.PermissionSync(state.Permissions, state.SuperUsers);
        permPacket.Send(whoAmI);
        NetLogger.State($"PermissionSync sent to player {whoAmI}");
    }
}
