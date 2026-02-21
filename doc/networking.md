# Networking System

## Packet Types

```
PacketType (byte enum)
---------------------------------------
Playback Control:
  1  PlaySong             5  SeekPosition
  2  StopSong             6  SlowedReverb
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

Event Triggers (Boss/Death):
  50 SetBossTrack         52 SetBossSoundpad
  51 SetDeathTrack        53 SetDeathSoundpad
  54 PlaySoundpadSound
```

## Packet Routing

```
Terra_Namp.HandlePacket(BinaryReader, whoAmI)
  -> PacketRouter.HandlePacket(reader, whoAmI)
     -> reader.ReadByte() -> PacketType
     -> switch -> {Type}Handler.Handle()
```

## PacketBuilder

Static factory для `ModPacket`. Паттерн создания:
1. `Create(PacketType)` — пишет type byte первым
2. Записывает поля в фиксированном порядке
3. Возвращает packet — вызывающий вызывает `.Send()`

**String encoding:** `ushort length` + `UTF8 bytes` через `WriteString`/`ReadString`.

**Отправка пакетов:**
```csharp
PacketBuilder.PlaySong(...).Send();           // Клиент -> Сервер
PacketBuilder.PlaySong(...).Send(-1, whoAmI); // Сервер -> Все кроме whoAmI
PacketBuilder.PlaySong(...).Send(targetIdx);  // Сервер -> Конкретный клиент
```

## Song Identification

Треки идентифицируются по **MD5 hash** (16 байт) в сети. Локально хранятся с именем **UUID**.

```
ContentHash:   ComputeHash(path) -> byte[16]
               HashToHex(byte[]) -> string (32 hex chars)
               HexToHash(string) -> byte[16]
SongRegistry:  hashToUuid / uuidToHash словари, ScanCache(), RegisterSong()
               GetHashByUuid(uuid) -> hashHex
               GetUuidByHash(hashHex) -> uuid
```

**Формат .txt файла** (`{uuid}.txt` на клиенте, `{hashHex}.txt` на сервере):
```
Line 0: Song title
Line 1: Author/channel name
Line 2: MD5 hash hex (32 chars)
Line 3: Folder name (empty для YouTube, parent dir для folder imports)
```

**Именование файлов:** Клиент: `{uuid}.mp3/.txt`, Сервер: `{hashHex}.mp3/.txt`

**Получить hash+meta по UUID (для отправки пакета):**
```csharp
string hashHex = SongRegistry.Instance.GetHashByUuid(uuid); // null если нет в кэше
if (hashHex != null)
{
    byte[] hash = ContentHash.HexToHash(hashHex);
    string title = "", author = "";
    string txtPath = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");
    if (File.Exists(txtPath))
    {
        var lines = File.ReadAllLines(txtPath);
        if (lines.Length >= 1) title = lines[0];
        if (lines.Length >= 2) author = lines[1];
    }
    PacketBuilder.SetBossTrack((byte)Main.myPlayer, hash, title, author).Send();
}
```

**Получить UUID по hash (в хэндлере на клиенте):**
```csharp
string uuid = SongRegistry.Instance.GetUuidByHash(hashHex); // null если файл не скачан
```

## Handler Pattern

Все хэндлеры следуют двухпутевому паттерну: сервер — переброс + обновление состояния, клиент — применение локально.

```csharp
using Terra_Namp.Content.IO;   // TerraDataStore, SoundpadDataStore
using Terra_Namp.Core.IO;      // PersistentDataStoreSystem

public static class MyHandler
{
    public static void Handle(BinaryReader reader, int whoAmI)
    {
        // 1. Читаем поля в том же порядке, что писал PacketBuilder
        byte sender = reader.ReadByte();
        byte[] hash = reader.ReadBytes(16);          // всегда 16 байт
        string title = PacketBuilder.ReadString(reader);
        string author = PacketBuilder.ReadString(reader);

        if (Main.netMode == NetmodeID.Server)
        {
            // Проверка прав (если нужно)
            if (!ServerJukeboxState.Instance.GetPermissions(whoAmI).CanManage)
                return;

            // Обновить ServerJukeboxState
            ServerJukeboxState.Instance.SomeField = value;

            // Перебросить всем клиентам кроме отправителя
            PacketBuilder.MyPacket(sender, ...).Send(-1, whoAmI);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            // Применить через QueueMainThreadAction если нужен доступ к UI/Audio
            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                panel?.DoSomething();
            });

            // Обновить DataStore напрямую (без QueueMainThreadAction)
            var store = PersistentDataStoreSystem.GetDataStore<TerraDataStore>();
            store.SomeField = value;
            store.ForceSave();
        }
    }
}
```

**ВАЖНО:** `using Terra_Namp.Content.IO` и `using Terra_Namp.Core.IO` — обязательны для доступа к хранилищам. Нельзя использовать полные пути вида `Terra_Namp.Core.IO.X` — компилятор парсит `Terra_Namp` как имя класса мода, а не namespace.

**Добавление нового пакета — чеклист:**
1. `PacketType.cs` — добавить константу с уникальным номером
2. `PacketBuilder.cs` — добавить статический метод-билдер
3. `Handlers/MyHandler.cs` — создать файл по паттерну выше
4. `PacketRouter.cs` — добавить `case PacketType.My: MyHandler.Handle(reader, whoAmI); break;`
5. Вызов: `PacketBuilder.My(...).Send()` из клиентского кода

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

**Параметры:** 8KB чанки, 4/тик, ~32KB/тик = ~1.9 MB/sec при 60 FPS.

**Server relay:** Сервер создаёт `ServerTransfer`, перебрасывает чанки в реальном времени, кэширует файл при завершении.

**PlaySong с sender=255 (сервер-источник):**
Сервер может сам инициировать воспроизведение (напр. для событий босса/смерти):
```csharp
// Серверный код (PostUpdateWorld или хэндлер)
jukeboxState.StartPlayback(hash, title, author, -1, false); // djIndex = -1
PacketBuilder.PlaySong(255, hash, title, author, false).Send(); // 255 = server
```
Клиент получает `PlaySong` от 255 → обрабатывает в ветке `MultiplayerClient` PlaySongHandler → если файл есть в кэше, играет; если нет — отправляет `RequestSong` на сервер (стандартный transfer flow). Проверки прав на клиенте нет.

## Boss/Death Event Architecture

Триггеры (босс/смерть) работают **по-разному в зависимости от netMode**:

| netMode | Где детектируется | Как воспроизводится |
|---------|-------------------|---------------------|
| SinglePlayer | `PostUpdateInput` (клиент) | `BeginPlayingSongLocalOnly(uuid)` / `SoundpadPlayback.PlaySound(uuid)` |
| Server (MP) | `PostUpdateWorld` (сервер) | `PacketBuilder.PlaySong(255,...).Send()` / `PacketBuilder.PlaySoundpadSound(uuid).Send()` |

**Mutual exclusivity:** Для каждого триггера активно ТОЛЬКО одно — либо музыкальный трек, либо звук саундпада. Установка одного сбрасывает другое:
- `SetBossTrack` handler → `state.BossSoundpadUuid = ""`
- `SetBossSoundpad` handler → `state.BossMusicHash = null`
- То же самое в `TerraDataStore` / `SoundpadDataStore` на клиентах

**Server-side состояние в `ServerJukeboxState`:**
```
BossMusicHash / BossMusicTitle / BossMusicAuthor  — трек для событий босса
DeathMusicHash / DeathMusicTitle / DeathMusicAuthor — трек для событий смерти
BossSoundpadUuid   — UUID саундпад-звука для босса (взаимоисключает BossMusicHash)
DeathSoundpadUuid  — UUID саундпад-звука для смерти (взаимоисключает DeathMusicHash)
WasAnyBossAlive    — предыдущий тик: был ли живой босс
ServerWasPlayerDead[] — предыдущий тик: был ли мёртв каждый игрок
DeathMusicTimer    — таймер в тиках до остановки музыки смерти
```

**Установка через UI → пакет на сервер:**
```csharp
// Когда пользователь выбирает "Set as Boss Music" через контекстное меню
if (Main.netMode == NetmodeID.MultiplayerClient)
    PacketBuilder.SetBossTrack((byte)Main.myPlayer, hash, title, author).Send();
// В Single Player — только сохранить в TerraDataStore.BossMusicUuid
```

## PlaySoundpadSound (54)

Саундпад-звуки хранятся **только локально** (нет сетевого transfer). Сервер рассылает UUID → каждый клиент воспроизводит у себя если файл есть.

```
SERVER                      CLIENT
  |   PlaySoundpadSound(uuid)  |
  |--------------------------->|
  |        [проверяет локальный кэш саундпада]
  |        [SoundpadPlayback.PlaySound(uuid) если файл существует]
```

## Prefetch System

DJ отправляет `PrefetchList` после нажатия Play со следующими 10 треками (hashes + metadata). Клиенты проверяют config `EnablePrefetch`, ставят в очередь через `SongTransferManager.QueuePrefetch()` (макс. 2 одновременно). Использует тот же RequestSong/SongChunk pipeline, но без `PendingPlayback`.

## Client Join Sync

```
TerraModPlayer.OnEnterWorld()
  -> PacketBuilder.RequestState(myPlayer).Send()
  -> RequestStateHandler (server): читает ServerJukeboxState, шлёт SyncState
  -> SyncStateHandler (client): play + seek, или SetPendingPlayback() + RequestSong()
```

## ServerJukeboxState

Server-side singleton (ModSystem). Ключевые поля:

```
CurrentSongHash     — текущий играющий трек
IsPlaying / IsPaused / IsForced
Title / Author
DjPlayerIndex       — индекс DJ (кто запустил трек; -1 если сервер)
LastKnownProgress / PlayStartTimeTicks / TotalPausedTicks
SlowedReverbEnabled
Permissions         — Dictionary<int, PlayerPermissions> (Listener/Controller/Admin)
SuperUsers          — HashSet<int>
IsDedicatedServer   — определяется при Load() по пути процесса
```

**Методы:** `StartPlayback(hash, title, author, djIndex, forced)`, `StopPlayback()`, `Pause()`, `Resume()`, `GetPermissions(playerIndex)`, `EnsurePlayerRegistered(playerIndex)`.

## Permissions

```
PermissionRole: Listener(0) < Controller(1) < Admin(2)

PlayerPermissions:
  CanPlay   -> Role >= Controller
  CanStop   -> Role >= Controller
  CanManage -> Role >= Admin
```

Self-host: первый подключившийся игрок → SuperUser + Admin.
Dedicated server: по умолчанию все Listener, Admin назначается через консоль.

## Packet Formats

| Пакет | Формат |
|-------|--------|
| PlaySong(1) | `[type] [sender] [hash:16] [title:str] [author:str] [forced:bool]` |
| StopSong(2) | `[type] [sender]` |
| PauseSong(3) | `[type] [sender]` |
| ResumeSong(4) | `[type] [sender]` |
| SeekPosition(5) | `[type] [sender] [progress:float]` |
| SlowedReverb(6) | `[type] [sender] [enabled:bool]` |
| RequestSong(10) | `[type] [requester] [hash:16]` |
| SongHeader(11) | `[type] [hash:16] [totalSize:int] [title:str] [author:str]` |
| SongChunk(12) | `[type] [hash:16] [chunkIndex:int] [chunkSize:ushort] [data:bytes]` |
| SongTransferComplete(13) | `[type] [hash:16]` |
| SyncState(20) | `[type] [isPlaying:bool] [isPaused:bool] [hash:16] [progress:float] [title:str] [author:str] [forced:bool] [slowedReverb:bool]` |
| RequestState(21) | `[type] [requester]` |
| PrefetchList(30) | `[type] [sender] [count:ushort] [repeat: hash:16 + title:str + author:str]` |
| PermissionUpdate(40) | `[type] [sender] [targetPlayer] [role:byte]` |
| PermissionSync(41) | `[type] [superUserCount:byte] [repeat: playerIndex:byte] [permCount:byte] [repeat: playerIndex:byte + role:byte]` |
| SetBossTrack(50) | `[type] [sender] [hash:16] [title:str] [author:str]` |
| SetDeathTrack(51) | `[type] [sender] [hash:16] [title:str] [author:str]` |
| SetBossSoundpad(52) | `[type] [sender] [uuid:str]` |
| SetDeathSoundpad(53) | `[type] [sender] [uuid:str]` |
| PlaySoundpadSound(54) | `[type] [uuid:str]` |

**String format:** `[ushort len] [byte[] utf8]`
