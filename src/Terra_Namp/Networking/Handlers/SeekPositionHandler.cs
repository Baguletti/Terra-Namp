using System.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SeekPositionHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();
        float progress = reader.ReadSingle();

        if (Main.netMode == NetmodeID.Server)
        {
            if (!ServerJukeboxState.Instance.GetPermissions(whoAmI).CanPlay)
            {
                NetLogger.Packet($"SeekPosition DENIED for player {whoAmI} (no permission)");
                return;
            }

            NetLogger.Packet($"SeekPosition from player {whoAmI}: progress={progress:F3}");
            ServerJukeboxState.Instance.LastKnownProgress = progress;

            var packet = PacketBuilder.SeekPosition(sender, progress);
            packet.Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Packet($"SeekPosition received from player {sender}: progress={progress:F3}");
            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                panel?.ActiveSong?.SeekFromNetwork(progress);
            });
        }
    }
}
