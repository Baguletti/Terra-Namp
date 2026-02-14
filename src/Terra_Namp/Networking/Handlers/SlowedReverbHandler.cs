using System.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class SlowedReverbHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();
        bool enabled = reader.ReadBoolean();

        if (Main.netMode == NetmodeID.Server)
        {
            if (!ServerJukeboxState.Instance.GetPermissions(whoAmI).CanPlay)
            {
                NetLogger.Packet($"SlowedReverb DENIED for player {whoAmI} (no permission)");
                return;
            }

            NetLogger.Packet($"SlowedReverb from player {whoAmI}: enabled={enabled}");
            ServerJukeboxState.Instance.SlowedReverbEnabled = enabled;

            var packet = PacketBuilder.SlowedReverb(sender, enabled);
            packet.Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Packet($"SlowedReverb received from player {sender}: enabled={enabled}");
            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                if (panel != null)
                {
                    panel.SlowedReverbActive = enabled;
                    panel.ActiveSong?.ApplySlowedReverbFromNetwork(enabled);
                }
            });
        }
    }
}
