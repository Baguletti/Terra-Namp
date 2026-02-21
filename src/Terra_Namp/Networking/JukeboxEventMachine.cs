using System.Collections.Generic;

namespace Terra_Namp.Networking;

/// <summary>
/// Pure state machine for boss/death music event logic.
/// No tModLoader or Terraria dependencies — fully unit-testable.
///
/// TIMING AND DESYNC NOTES:
///
/// This machine runs on the server every tick. When an event fires it returns events
/// that the caller (PostUpdateWorld) translates into Send() calls. All clients receive
/// the packet in the same server tick, but each client experiences it at different times:
///
///   Server tick N → PacketBuilder.PlaySong.Send()
///      └─ Client A (20ms ping)  → receives at ~N+1.2 ticks → QueueMainThreadAction → N+2 local frame
///      └─ Client B (80ms ping)  → receives at ~N+4.8 ticks → QueueMainThreadAction → N+5 local frame
///
///   Net desync ≈ |ping_A - ping_B| / 16.6ms frames
///
/// On one PC (loopback ≈ 0ms): desync comes from OS thread scheduling (~0-2 frames = 0-33ms).
/// In real multiplayer: desync = ping difference between clients. If A has 30ms and B has 120ms
/// → ~5 frame / ~90ms desync. The audio stream also starts independently on each client
/// (OS audio buffer init ≈ 50-200ms variance) which is the dominant source of perceived drift.
///
/// What tests CAN verify: correct event sequence, correct timer duration, no duplicate events.
/// What tests CANNOT verify: wall-clock synchronization between clients (network concern).
/// </summary>
public class JukeboxEventMachine
{
    // Config passed each tick (read-only snapshot from DataStore/ServerJukeboxState)
    public record JukeboxConfig(
        byte[] BossTrackHash,
        string BossSoundpadUuid,
        byte[] DeathTrackHash,
        string DeathSoundpadUuid);

    public record GameState(bool[] PlayerDead, bool AnyBossAlive);

    public enum EventType
    {
        PlayBossTrack,
        PlayBossSoundpad,
        StopBossTrack,
        PlayDeathTrack,
        PlayDeathSoundpad,
        StopDeathTrack,
    }

    public record Event(EventType Type);

    // --- Internal state ---
    private bool _wasAnyBossAlive;
    private readonly bool[] _wasPlayerDead;
    private readonly int _maxPlayers;

    private bool _isPlayingBossMusic;
    private bool _isPlayingDeathMusic;

    public int DeathMusicTimer { get; private set; }
    public const int DeathMusicDurationTicks = 300; // 5s at 60fps

    public JukeboxEventMachine(int maxPlayers = 255)
    {
        _maxPlayers = maxPlayers;
        _wasPlayerDead = new bool[maxPlayers];
    }

    /// <summary>
    /// Advance one server tick. Returns events that should be broadcast to all clients.
    /// Caller is responsible for translating events to packets and calling Send().
    /// </summary>
    public IReadOnlyList<Event> Tick(GameState state, JukeboxConfig config)
    {
        var events = new List<Event>();

        TickBoss(state.AnyBossAlive, config, events);
        TickDeathTimer(config, events); // tick existing timer BEFORE processing new deaths
        TickDeath(state.PlayerDead, config, events);

        return events;
    }

    private void TickBoss(bool anyBoss, JukeboxConfig config, List<Event> events)
    {
        if (anyBoss && !_wasAnyBossAlive)
        {
            // Boss just spawned
            if (config.BossTrackHash != null)
            {
                events.Add(new Event(EventType.PlayBossTrack));
                _isPlayingBossMusic = true;
            }
            else if (!string.IsNullOrEmpty(config.BossSoundpadUuid))
            {
                events.Add(new Event(EventType.PlayBossSoundpad));
            }
        }
        else if (!anyBoss && _wasAnyBossAlive)
        {
            // Boss just died/despawned
            if (_isPlayingBossMusic)
            {
                events.Add(new Event(EventType.StopBossTrack));
                _isPlayingBossMusic = false;
            }
        }

        _wasAnyBossAlive = anyBoss;
    }

    private void TickDeath(bool[] playerDead, JukeboxConfig config, List<Event> events)
    {
        // Scan all players — but emit at most ONE death event per tick.
        // If two players die simultaneously, only the first triggers music to avoid
        // sending duplicate PlaySong packets that would restart the track for all clients.
        bool anyJustDied = false;

        for (int i = 0; i < _maxPlayers; i++)
        {
            bool isDead = i < playerDead.Length && playerDead[i];

            if (isDead && !_wasPlayerDead[i] && !anyJustDied)
            {
                anyJustDied = true;

                if (config.DeathTrackHash != null)
                {
                    events.Add(new Event(EventType.PlayDeathTrack));
                    DeathMusicTimer = DeathMusicDurationTicks;
                    _isPlayingDeathMusic = true;
                }
                else if (!string.IsNullOrEmpty(config.DeathSoundpadUuid))
                {
                    events.Add(new Event(EventType.PlayDeathSoundpad));
                    // No timer for soundpad — plays once and finishes on its own
                }
            }

            _wasPlayerDead[i] = isDead;
        }
    }

    private void TickDeathTimer(JukeboxConfig config, List<Event> events)
    {
        if (DeathMusicTimer <= 0) return;

        DeathMusicTimer--;

        if (DeathMusicTimer == 0 && _isPlayingDeathMusic)
        {
            events.Add(new Event(EventType.StopDeathTrack));
            _isPlayingDeathMusic = false;
        }
    }

    // For testing: force-set internal state
    internal void SetIsPlayingBossMusic(bool v) => _isPlayingBossMusic = v;
    internal void SetIsPlayingDeathMusic(bool v) => _isPlayingDeathMusic = v;
}
