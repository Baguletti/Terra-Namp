# Admin & Permission System

## Overview

In multiplayer, the permission system controls who can manage music playback. The host (or a designated admin) assigns roles to players via an in-game admin panel.

**Single player:** Permission system is not active. All actions go through local methods without packets or checks. Shield button is hidden.

## Permission Model

### Role Hierarchy

```
Admin (2)       — full control + manage other players' permissions
Controller (1)  — play/pause/stop/seek/download
Listener (0)    — can only hear music (default)
```

### Computed Permissions

```csharp
public bool CanPlay => Role >= PermissionRole.Controller;
public bool CanStop => Role >= PermissionRole.Controller;
public bool CanManage => Role >= PermissionRole.Admin;
```

### Defaults

- All new players: `Role = Listener`

## Super User

Super user — администратор с неизменяемыми правами. Его права невозможно изменить: ни через UI, ни через пакеты. У суперюзера нет кнопок в админ-панели — вместо них надпись "Super Admin".

**Может быть несколько super user-ов.** На self-host это всегда один (хост). На dedicated сервере оператор может назначить нескольких через консоль.

### Логика назначения

**Не сохраняется на диск.** Рассчитывается при каждом старте сервера.

```
ServerJukeboxState.SuperUsers = {}  (при старте)
```

**Self-host (Host&Play):**
Первый игрок, зашедший на сервер → автоматически Admin + Super User.

```
EnsurePlayerRegistered(playerIndex):
    if SuperUsers.Count == 0:
        role = Admin
        SuperUsers.Add(playerIndex)
    else:
        role = Listener
```

**Dedicated server:**
Первый игрок НЕ получает автоматически никаких прав.
Администратор сервера использует консольные команды.
Первая выданная админка через `terra-namp-admin` = Super User.
Дополнительные super user-ы через `terra-namp-superuser`.

### Защита

- **Сервер:** `PermissionUpdateHandler` блокирует любые изменения для `state.IsSuperUser(targetPlayer)`
- **Клиент:** `AdminPanel` не показывает кнопки для суперюзеров (только label "Super Admin")
- **Самомодификация:** Заблокирована для всех, включая суперюзеров

## Console Commands (Dedicated Server)

Все команды выполняются в серверной консоли tModLoader.

### `terra-namp-admin <player_name>`

Назначает игрока администратором Terra Namp.

**Параметры:**
- `player_name` — имя персонажа (case-insensitive, точное совпадение)

**Поведение:**
1. Находит активного игрока по имени
2. Устанавливает `Role = Admin`
3. Если это первый админ на сервере → также назначает Super User
4. Рассылает `PermissionSync` всем клиентам

**Примеры:**
```
terra-namp-admin PlayerName
terra-namp-admin Player With Spaces
```

**Вывод:**
```
Player "PlayerName" (index 0) is now a Terra Namp admin (super user).
Player "AnotherPlayer" (index 1) is now a Terra Namp admin.
```

### `terra-namp-superuser <player_name>`

Назначает игрока super user-ом (неизменяемый админ).

**Параметры:**
- `player_name` — имя персонажа (case-insensitive, точное совпадение)

**Поведение:**
1. Находит активного игрока по имени
2. Устанавливает `Role = Admin`
3. Добавляет в множество Super Users
4. Рассылает `PermissionSync` всем клиентам

**Примеры:**
```
terra-namp-superuser TrustedModerator
```

**Вывод:**
```
Player "TrustedModerator" (index 2) is now a Terra Namp super user.
```

**Отличие от `terra-namp-admin`:** Super user-а невозможно понизить через UI. Обычного админа другой админ может переключить обратно в Controller/Listener.

### Сценарии использования

**Типичный self-host:**
1. Хост создаёт сервер через Host&Play
2. Хост заходит первым → автоматически Super User + Admin
3. Другие игроки заходят → Listener по умолчанию
4. Хост через UI (щит) повышает нужных игроков до Controller/Admin

**Типичный dedicated server:**
1. Сервер запускается, никто не admin
2. Первый игрок заходит → Listener (не может управлять плеером)
3. Оператор в консоли: `terra-namp-admin TrustedPlayer`
4. TrustedPlayer получает Admin + Super User (первый админ)
5. TrustedPlayer через UI может повышать других
6. Оператор может добавить ещё админов: `terra-namp-admin AnotherPlayer`
7. Для защиты от понижения: `terra-namp-superuser TrustedModerator`

**Рестарт сервера:**
1. Все права сбрасываются
2. Self-host: первый зашедший снова Super User
3. Dedicated: нужно заново выдать через консоль

## Permission Enforcement

### Двойная проверка (клиент + сервер)

Права проверяются в двух местах:

**Клиент (превентивная):**
- `BeginPlayingSong()` — не запускает трек если `!CanPlay`
- `Toggle()` — не ставит на паузу / не возобновляет
- `Skip()`, `SeekToProgress()` — не перематывает
- Если нет прав → действие не выполняется, пакет не отправляется

**Сервер (авторитетная):**

| Handler | Check |
|---------|-------|
| PlaySongHandler | `CanPlay` |
| StopSongHandler | `CanStop` |
| PauseSongHandler | `CanPlay` |
| ResumeSongHandler | `CanPlay` |
| SeekPositionHandler | `CanPlay` |
| PermissionUpdateHandler | `CanManage` + not self + not super user |

Серверная проверка — окончательная. Даже если клиент обойдёт локальную проверку (модифицированный клиент), сервер отклонит пакет.

## Network Protocol

### Packets

| Type | ID | Direction | Purpose |
|------|-----|-----------|---------|
| PermissionUpdate | 40 | Client → Server | Admin requests permission change for a player |
| PermissionSync | 41 | Server → Clients | Broadcast full permission map to all clients |

### PermissionUpdate (40)

```
[byte type=40] [byte sender] [byte targetPlayer] [byte role]
```

Server-side validation:
1. Sender must have `CanManage` (role >= Admin)
2. Cannot modify own permissions (`targetPlayer != whoAmI`)
3. Cannot modify super user (`state.IsSuperUser(targetPlayer)`)
4. If valid: update permissions, broadcast `PermissionSync`

### PermissionSync (41)

```
[byte type=41] [byte superCount] [byte[] superUserIndices] [byte permCount] [repeat: byte playerIndex, byte role]
```

Sent to all clients when:
- A permission is changed (via UI or console)
- A new player joins

Client stores the data in `ClientPermissionCache`.

## Client-Side Cache

`ClientPermissionCache` (static class) mirrors server permissions on the client:

```csharp
public static Dictionary<int, PlayerPermissions> Permissions { get; }
public static HashSet<int> SuperUsers { get; }
public static bool IsLocalPlayerAdmin();
public static bool IsSuperUser(int playerIndex);
public static PlayerPermissions GetLocalPermissions();
public static void Clear();  // Called on disconnect
```

## Admin Panel UI

### Access

Shield button (`Shield.png`) in the title bar, next to settings gear. Visible only when:
- `Main.netMode == MultiplayerClient` (multiplayer)
- `ClientPermissionCache.IsLocalPlayerAdmin()` (player is admin)

Button always `Append`-ed (for texture loading), hidden offscreen when not applicable.

### Player List

| Player Type | Buttons | Label |
|-------------|---------|-------|
| Super User | none | "Super Admin" |
| Self (you) | none | role name |
| Other player | Access + Role | — |

### Access Button (Allow/Decline)

Controls whether a player can use the music player (Controller role).

| State | Icon | Color | Meaning |
|-------|------|-------|---------|
| Controller+ | `Allow.png` (checkmark) | green | Can control playback |
| Listener | `Decline.png` (X) | red | Can only hear music |

**Click behavior:**
- Checkmark (Controller/Admin) → demote to Listener
- X mark (Listener) → promote to Controller

### Admin Button (Star)

Controls admin access (manage permissions).

| State | Background | Border | Icon Color | Extra |
|-------|-----------|--------|------------|-------|
| Not admin | dim (5% white) | hover only | 30% white | — |
| Admin | 25% accent | accent 40% | full accent | pulsating glow |

**Click behavior:**
- Star OFF (Listener/Controller) → promote to Admin
- Star ON (Admin) → demote to Controller

Both buttons send `PermissionUpdate` packet to server.

## Icon Assets

All icons in `Assets/UI/Icons/`:
- `Shield.png` — admin panel button
- `Allow.png` — checkmark for allowed state
- `Decline.png` — X mark for denied state
- `User_star.png` — star icon for role indicator

**All icons must be white PNGs.** Code applies tinting via `spriteBatch.Draw(icon, rect, tintColor)`.
