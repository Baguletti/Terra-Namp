using System.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class StopSongHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();

        if (Main.netMode == NetmodeID.Server)
        {
            var state = ServerJukeboxState.Instance;

            if (!state.GetPermissions(whoAmI).CanStop)
            {
                NetLogger.Packet($"StopSong DENIED for player {whoAmI} (no permission)");
                return;
            }

            NetLogger.Packet($"StopSong from player {whoAmI}, clearing jukebox state");
            state.StopPlayback();

            var packet = PacketBuilder.StopSong(sender);
            packet.Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Packet($"StopSong received from player {sender}");
            bool isEventStop = sender == 255;
            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                if (isEventStop)
                    panel?.RestorePreEventState();
                else
                    panel?.StopCurrentSongFromNetwork();
            });
        }
    }
}
