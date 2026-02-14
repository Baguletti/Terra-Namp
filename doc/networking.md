# Networking System

## Packet Types

```
PacketType (byte enum)
---------------------------------------
Playback Control:
  1  PlaySong             5  SeekPosition
  2  StopSong
  3  PauseSong
  4  ResumeSong

Song Transfer:
  10 RequestSong          13 SongTransferComplete
  11 SongHeader
  12 SongChunk

State Sync:
  20 SyncState            21 RequestState

Prefetch:
  30 PrefetchList

Permissions:
  40 PermissionUpdate     41 PermissionSync
```

## Packet Routing

```
Terra_Namp.HandlePacket(BinaryReader, whoAmI)
  -> PacketRouter.HandlePacket(reader, whoAmI)
     -> reader.ReadByte() -> PacketType
     -> switch -> {Type}Handler.Handle()
```

## PacketBuilder

Static factory for `ModPacket` instances. Each method:
1. `Create(PacketType)` writes type byte first
2. Writes fields in order
3. Returns packet (caller calls `.Send()`)

**String encoding:** `ushort length` + `UTF8 bytes` via `WriteString`/`ReadString`.

## Song Identification

Songs identified across network by **MD5 hash** (16 bytes). Locally stored with **UUID** filename.

```
ContentHash:   ComputeHash(path) -> byte[16], HashToHex/HexToHash
SongRegistry:  hashToUuid / uuidToHash mappings, ScanCache(), RegisterSong()
```

**Cache file format** (`{name}.txt`):
```
Line 0: Song title
Line 1: Author/channel name
Line 2: MD5 hash hex (32 chars)
Line 3: Folder name (empty for YouTube, parent dir for folder imports)
```

**Naming:** Client: `{uuid}.mp3/.txt`, Server: `{hashHex}.mp3/.txt`

## Song Transfer Flow

```
CLIENT A (DJ)                SERVER                  CLIENT B (listener)
    |                           |                          |
    | PlaySong(hash,meta)       |                          |
    |-------------------------->| PlaySong (rebroadcast)   |
    |                           |------------------------->|
    |                           |     [B checks local cache]
    |                           |  RequestSong(hash)       |
    |                           |<-------------------------|
    |   [Server cache miss]     |                          |
    |   RequestSong(hash)       |                          |
    |<--------------------------|                          |
    |   SongHeader              |  SongHeader (relay)      |
    |-------------------------->|------------------------->|
    |   SongChunk * N           |  SongChunk (relay)       |
    |-------------------------->|------------------------->|
    |   (4 chunks/tick, 8KB ea) |                          |
    |   SongTransferComplete    |  SongTransferComplete    |
    |-------------------------->|------------------------->|
```

**Parameters:** 8KB chunks, 4/tick, ~32KB/tick = ~1.9 MB/sec at 60 FPS.

**Server relay:** Server creates `ServerTransfer`, forwards chunks in real-time, caches file on completion.

## Prefetch System

DJ sends `PrefetchList` packet after pressing Play with next 10 upcoming songs (hashes + metadata). Clients check `EnablePrefetch` config, queue missing songs via `SongTransferManager.QueuePrefetch()` (max 2 concurrent). Uses same RequestSong/SongChunk pipeline, no `PendingPlayback` set.

## Handler Pattern

All handlers follow dual-path:
```csharp
public static void Handle(BinaryReader reader, int whoAmI)
{
    byte sender = reader.ReadByte();
    if (Main.netMode == NetmodeID.Server)
    {
        // Update ServerJukeboxState
        // Rebroadcast: packet.Send(-1, whoAmI)
    }
    else if (Main.netMode == NetmodeID.MultiplayerClient)
    {
        // Apply locally via Main.QueueMainThreadAction()
    }
}
```

## Client Join Sync

```
TerraModPlayer.OnEnterWorld()
  -> PacketBuilder.RequestState(myPlayer).Send()
  -> RequestStateHandler (server): reads ServerJukeboxState, sends SyncState
  -> SyncStateHandler (client): play + seek, or SetPendingPlayback() + RequestSong()
```

## ServerJukeboxState

Server-side singleton: `CurrentSongHash`, `IsPlaying`, `IsPaused`, `IsForced`, `Title`, `Author`, `DjPlayerIndex`, `LastKnownProgress`, timing ticks, `Permissions` per player.

## Packet Formats

| Packet | Format |
|--------|--------|
| PlaySong(1) | `[type] [sender] [hash:16] [title:str] [author:str] [forced:bool]` |
| StopSong(2) | `[type] [sender]` |
| PauseSong(3) | `[type] [sender]` |
| ResumeSong(4) | `[type] [sender]` |
| SeekPosition(5) | `[type] [sender] [progress:float]` |
| RequestSong(10) | `[type] [requester] [hash:16]` |
| SongHeader(11) | `[type] [hash:16] [totalSize:int] [title:str] [author:str]` |
| SongChunk(12) | `[type] [hash:16] [chunkIndex:ushort] [chunkSize:ushort] [data]` |
| SongTransferComplete(13) | `[type] [hash:16]` |
| SyncState(20) | `[type] [isPlaying] [isPaused] [hash:16] [progress:float] [title:str] [author:str] [forced]` |
| RequestState(21) | `[type] [requester]` |
| PrefetchList(30) | `[type] [sender] [count:byte] [repeat: hash:16 + title:str + author:str]` |
| PermissionUpdate(40) | `[type] [sender] [targetPlayer] [isAllowed:bool] [role:byte]` |
| PermissionSync(41) | `[type] [count:byte] [repeat: playerIndex:byte + isAllowed:bool + role:byte]` |

String format: `[ushort len] [byte[] utf8]`
