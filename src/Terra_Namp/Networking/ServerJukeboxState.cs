using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp.Networking;

public enum PermissionRole : byte
{
    Listener = 0,
    Controller = 1,
    Admin = 2,
}

public struct PlayerPermissions
{
    public PermissionRole Role;

    public bool CanPlay => Role >= PermissionRole.Controller;
    public bool CanStop => Role >= PermissionRole.Controller;
    public bool CanManage => Role >= PermissionRole.Admin;

    public static PlayerPermissions Default => new()
    {
        Role = PermissionRole.Listener,
    };
}

public class ServerJukeboxState : ModSystem
{
    public static ServerJukeboxState Instance { get; private set; }

    public byte[] CurrentSongHash { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsPaused { get; set; }
    public bool IsForced { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int DjPlayerIndex { get; set; } = -1;
    public float LastKnownProgress { get; set; }
    public long PlayStartTimeTicks { get; set; }
    public long PausedAtTick { get; set; }
    public long TotalPausedTicks { get; set; }
    public bool SlowedReverbEnabled { get; set; }

    // Boss/death track state (server stores these so it can trigger playback on events)
    public byte[] BossMusicHash { get; set; }
    public string BossMusicTitle { get; set; } = "";
    public string BossMusicAuthor { get; set; } = "";
    public byte[] DeathMusicHash { get; set; }
    public string DeathMusicTitle { get; set; } = "";
    public string DeathMusicAuthor { get; set; } = "";

    // Boss/death soundpad UUID (server stores, broadcasts PlaySoundpadSound on event)
    public string BossSoundpadUuid { get; set; } = "";
    public string DeathSoundpadUuid { get; set; } = "";

    // Kept for external query (e.g. packet handlers). Authoritative state lives in JukeboxEventMachine.
    public bool WasAnyBossAlive { get; set; }
    public int DeathMusicTimer { get; set; }

    public Dictionary<int, PlayerPermissions> Permissions { get; } = new();
    public HashSet<int> SuperUsers { get; } = new();

    // Main.dedServ is true in BOTH Host&Play and dedicated server.
    // Reliable detection: Host&Play runs from client binary (tModLoader),
    // dedicated server runs from server binary (tModLoaderServer).
    public bool IsDedicatedServer { get; private set; }

    public override void Load()
    {
        Instance = this;
        string processPath = System.Environment.ProcessPath ?? "";
        IsDedicatedServer = processPath.Contains("Server", System.StringComparison.OrdinalIgnoreCase);
        Terra_Namp.Instance?.Logger.Info($"ServerJukeboxState.Load: processPath=\"{System.IO.Path.GetFileName(processPath)}\" -> IsDedicatedServer={IsDedicatedServer}");
    }

    public override void Unload()
    {
        Instance = null;
    }

    public void StartPlayback(byte[] hash, string title, string author, int djIndex, bool forced)
    {
        string hashHex = ContentHash.HashToHex(hash);
        NetLogger.State($"StartPlayback: \"{title}\" by \"{author}\" hash={hashHex[..8]}.. dj=player{djIndex} forced={forced}");

        CurrentSongHash = hash;
        Title = title;
        Author = author;
        IsPlaying = true;
        IsPaused = false;
        IsForced = forced;
        DjPlayerIndex = djIndex;
        PlayStartTimeTicks = Main.GameUpdateCount;
        TotalPausedTicks = 0;
        LastKnownProgress = 0f;
    }

    public void StopPlayback()
    {
        NetLogger.State($"StopPlayback: was playing \"{Title}\" forced={IsForced}");
        CurrentSongHash = null;
        IsPlaying = false;
        IsPaused = false;
        IsForced = false;
        Title = "";
        Author = "";
        DjPlayerIndex = -1;
        LastKnownProgress = 0f;
        SlowedReverbEnabled = false;
    }

    public void Pause()
    {
        NetLogger.State($"Pause: \"{Title}\"");
        IsPaused = true;
        PausedAtTick = Main.GameUpdateCount;
    }

    public void Resume()
    {
        if (IsPaused)
        {
            TotalPausedTicks += Main.GameUpdateCount - PausedAtTick;
            IsPaused = false;
            NetLogger.State($"Resume: \"{Title}\" (was paused {Main.GameUpdateCount - PausedAtTick} ticks)");
        }
    }

    public PlayerPermissions GetPermissions(int playerIndex)
    {
        return Permissions.TryGetValue(playerIndex, out var perms) ? perms : PlayerPermissions.Default;
    }

    public void EnsurePlayerRegistered(int playerIndex)
    {
        if (Permissions.ContainsKey(playerIndex))
            return;

        // Self-host: first player = super user + admin.
        // Dedicated server: admin only via console command.
        // IsDedicatedServer is captured at Load() time (reliable, unlike Main.dedServ at packet time).
        bool autoAdmin = !IsDedicatedServer && SuperUsers.Count == 0;

        var role = autoAdmin ? PermissionRole.Admin : PermissionRole.Listener;
        NetLogger.State($"EnsurePlayerRegistered: player {playerIndex} -> role={role} autoAdmin={autoAdmin} isDedicated={IsDedicatedServer}");

        if (autoAdmin)
            SuperUsers.Add(playerIndex);

        Permissions[playerIndex] = new PlayerPermissions { Role = role };
    }

    public bool IsSuperUser(int playerIndex) => SuperUsers.Contains(playerIndex);
}
