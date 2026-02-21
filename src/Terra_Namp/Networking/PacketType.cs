namespace Terra_Namp.Networking;

public enum PacketType : byte
{
    PlaySong = 1,
    StopSong = 2,
    PauseSong = 3,
    ResumeSong = 4,
    SeekPosition = 5,
    SlowedReverb = 6,

    RequestSong = 10,
    SongHeader = 11,
    SongChunk = 12,
    SongTransferComplete = 13,

    SyncState = 20,
    RequestState = 21,

    PrefetchList = 30,

    PermissionUpdate = 40,
    PermissionSync = 41,

    SetBossTrack     = 50,
    SetDeathTrack    = 51,
    SetBossSoundpad  = 52,
    SetDeathSoundpad = 53,
    PlaySoundpadSound = 54,
}
