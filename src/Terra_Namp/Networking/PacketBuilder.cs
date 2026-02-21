using System.Collections.Generic;
using System.Text;
using Terraria.ModLoader;

namespace Terra_Namp.Networking;

public static class PacketBuilder
{
    private static ModPacket Create(PacketType type)
    {
        ModPacket packet = Terra_Namp.Instance.GetPacket();
        packet.Write((byte)type);
        return packet;
    }

    public static ModPacket PlaySong(byte senderIndex, byte[] hash, string title, string author, bool forced)
    {
        ModPacket packet = Create(PacketType.PlaySong);
        packet.Write(senderIndex);
        packet.Write(hash);
        WriteString(packet, title);
        WriteString(packet, author);
        packet.Write(forced);
        return packet;
    }

    public static ModPacket StopSong(byte senderIndex)
    {
        ModPacket packet = Create(PacketType.StopSong);
        packet.Write(senderIndex);
        return packet;
    }

    public static ModPacket PauseSong(byte senderIndex)
    {
        ModPacket packet = Create(PacketType.PauseSong);
        packet.Write(senderIndex);
        return packet;
    }

    public static ModPacket ResumeSong(byte senderIndex)
    {
        ModPacket packet = Create(PacketType.ResumeSong);
        packet.Write(senderIndex);
        return packet;
    }

    public static ModPacket SeekPosition(byte senderIndex, float progress)
    {
        ModPacket packet = Create(PacketType.SeekPosition);
        packet.Write(senderIndex);
        packet.Write(progress);
        return packet;
    }

    public static ModPacket SlowedReverb(byte senderIndex, bool enabled)
    {
        ModPacket packet = Create(PacketType.SlowedReverb);
        packet.Write(senderIndex);
        packet.Write(enabled);
        return packet;
    }

    public static ModPacket RequestSong(byte requesterIndex, byte[] hash)
    {
        ModPacket packet = Create(PacketType.RequestSong);
        packet.Write(requesterIndex);
        packet.Write(hash);
        return packet;
    }

    public static ModPacket SongHeader(byte[] hash, int totalSize, string title, string author)
    {
        ModPacket packet = Create(PacketType.SongHeader);
        packet.Write(hash);
        packet.Write(totalSize);
        WriteString(packet, title);
        WriteString(packet, author);
        return packet;
    }

    public static ModPacket SongChunk(byte[] hash, int chunkIndex, byte[] data, int length)
    {
        ModPacket packet = Create(PacketType.SongChunk);
        packet.Write(hash);
        packet.Write(chunkIndex);
        packet.Write((ushort)length);
        packet.Write(data, 0, length);
        return packet;
    }

    public static ModPacket SongTransferComplete(byte[] hash)
    {
        ModPacket packet = Create(PacketType.SongTransferComplete);
        packet.Write(hash);
        return packet;
    }

    public static ModPacket SyncState(bool isPlaying, bool isPaused, byte[] hash, float progress,
        string title, string author, bool forced, bool slowedReverb)
    {
        ModPacket packet = Create(PacketType.SyncState);
        packet.Write(isPlaying);
        packet.Write(isPaused);
        packet.Write(hash);
        packet.Write(progress);
        WriteString(packet, title);
        WriteString(packet, author);
        packet.Write(forced);
        packet.Write(slowedReverb);
        return packet;
    }

    public static ModPacket RequestState(byte requesterIndex)
    {
        ModPacket packet = Create(PacketType.RequestState);
        packet.Write(requesterIndex);
        return packet;
    }

    public static ModPacket PrefetchList(byte senderIndex, List<(byte[] Hash, string Title, string Author)> songs)
    {
        ModPacket packet = Create(PacketType.PrefetchList);
        packet.Write(senderIndex);
        packet.Write((ushort)songs.Count);
        foreach (var (hash, title, author) in songs)
        {
            packet.Write(hash);
            WriteString(packet, title);
            WriteString(packet, author);
        }
        return packet;
    }

    public static ModPacket PermissionUpdate(byte senderIndex, byte targetPlayer, PermissionRole role)
    {
        ModPacket packet = Create(PacketType.PermissionUpdate);
        packet.Write(senderIndex);
        packet.Write(targetPlayer);
        packet.Write((byte)role);
        return packet;
    }

    public static ModPacket PermissionSync(Dictionary<int, PlayerPermissions> permissions, HashSet<int> superUsers)
    {
        ModPacket packet = Create(PacketType.PermissionSync);
        packet.Write((byte)superUsers.Count);
        foreach (int idx in superUsers)
            packet.Write((byte)idx);
        packet.Write((byte)permissions.Count);
        foreach (var (playerIndex, perms) in permissions)
        {
            packet.Write((byte)playerIndex);
            packet.Write((byte)perms.Role);
        }
        return packet;
    }

    public static ModPacket SetBossTrack(byte senderIndex, byte[] hash, string title, string author)
    {
        ModPacket packet = Create(PacketType.SetBossTrack);
        packet.Write(senderIndex);
        packet.Write(hash);
        WriteString(packet, title);
        WriteString(packet, author);
        return packet;
    }

    public static ModPacket SetDeathTrack(byte senderIndex, byte[] hash, string title, string author)
    {
        ModPacket packet = Create(PacketType.SetDeathTrack);
        packet.Write(senderIndex);
        packet.Write(hash);
        WriteString(packet, title);
        WriteString(packet, author);
        return packet;
    }

    public static ModPacket SetBossSoundpad(byte senderIndex, string uuid)
    {
        ModPacket packet = Create(PacketType.SetBossSoundpad);
        packet.Write(senderIndex);
        WriteString(packet, uuid);
        return packet;
    }

    public static ModPacket SetDeathSoundpad(byte senderIndex, string uuid)
    {
        ModPacket packet = Create(PacketType.SetDeathSoundpad);
        packet.Write(senderIndex);
        WriteString(packet, uuid);
        return packet;
    }

    public static ModPacket PlaySoundpadSound(string uuid)
    {
        ModPacket packet = Create(PacketType.PlaySoundpadSound);
        WriteString(packet, uuid);
        return packet;
    }

    private static void WriteString(ModPacket packet, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        packet.Write((ushort)bytes.Length);
        packet.Write(bytes);
    }

    public static string ReadString(System.IO.BinaryReader reader)
    {
        ushort length = reader.ReadUInt16();
        byte[] bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
}
