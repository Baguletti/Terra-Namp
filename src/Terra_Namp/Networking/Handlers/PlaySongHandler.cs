using System.IO;
using Terra_Namp.Content.Audio;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp.Networking.Handlers;

public static class PlaySongHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        byte sender = reader.ReadByte();
        byte[] hash = reader.ReadBytes(16);
        string title = PacketBuilder.ReadString(reader);
        string author = PacketBuilder.ReadString(reader);
        bool forced = reader.ReadBoolean();
        string hashHex = ContentHash.HashToHex(hash);

        if (Main.netMode == NetmodeID.Server)
        {
            var state = ServerJukeboxState.Instance;

            if (!state.GetPermissions(whoAmI).CanPlay)
            {
                NetLogger.Packet($"PlaySong DENIED for player {whoAmI} (no permission)");
                return;
            }

            NetLogger.Packet($"PlaySong from player {whoAmI}: \"{title}\" by {author} hash={hashHex[..8]}.. forced={forced}");
            state.StartPlayback(hash, title, author, whoAmI, forced);

            var packet = PacketBuilder.PlaySong(sender, hash, title, author, forced);
            packet.Send(-1, whoAmI);
            NetLogger.Packet($"PlaySong rebroadcast to all except {whoAmI}");
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetLogger.Packet($"PlaySong received: \"{title}\" by {author} hash={hashHex[..8]}.. forced={forced}");
            string localUuid = SongRegistry.Instance.GetUuidByHash(hashHex);

            if (localUuid != null)
            {
                NetLogger.Info($"Song found in local cache: uuid={localUuid[..8]}.. Starting playback");
                Main.QueueMainThreadAction(() =>
                {
                    var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                    panel?.BeginPlayingSongFromNetwork(localUuid, forced);

                    if (forced)
                        ModContent.GetInstance<TerraTrackUpdaterSystem>().CurrentlyForcingSong = true;
                });
            }
            else
            {
                NetLogger.Transfer($"Song NOT in cache, requesting transfer for hash={hashHex[..8]}..");
                SongTransferManager.Instance?.SetPendingPlayback(hashHex, title, author, forced);

                var requestPacket = PacketBuilder.RequestSong((byte)Main.myPlayer, hash);
                requestPacket.Send();
            }
        }
    }
}
