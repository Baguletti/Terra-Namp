using System.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;

namespace Terra_Namp.Networking.Handlers;

public static class PlaySoundpadSoundHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        string uuid = PacketBuilder.ReadString(reader);

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.State($"[PlaySoundpadSound] received uuid={uuid[..8]}..");
            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                panel?.SoundpadPlayback?.PlaySound(uuid);
            });
        }
    }
}
