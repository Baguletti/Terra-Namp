using System.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class PauseSongHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();

        if (Main.netMode == NetmodeID.Server)
        {
            if (!ServerJukeboxState.Instance.GetPermissions(whoAmI).CanPlay)
            {
                NetLogger.Packet($"PauseSong DENIED for player {whoAmI} (no permission)");
                return;
            }

            NetLogger.Packet($"PauseSong from player {whoAmI}");
            ServerJukeboxState.Instance.Pause();

            var packet = PacketBuilder.PauseSong(sender);
            packet.Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Packet($"PauseSong received from player {sender}");
            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                panel?.ActiveSong?.PauseFromNetwork();
            });
        }
    }
}
