using System.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class ResumeSongHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();

        if (Main.netMode == NetmodeID.Server)
        {
            if (!ServerJukeboxState.Instance.GetPermissions(whoAmI).CanPlay)
            {
                NetLogger.Packet($"ResumeSong DENIED for player {whoAmI} (no permission)");
                return;
            }

            NetLogger.Packet($"ResumeSong from player {whoAmI}");
            ServerJukeboxState.Instance.Resume();

            var packet = PacketBuilder.ResumeSong(sender);
            packet.Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Packet($"ResumeSong received from player {sender}");
            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                panel?.ActiveSong?.ResumeFromNetwork();
            });
        }
    }
}
