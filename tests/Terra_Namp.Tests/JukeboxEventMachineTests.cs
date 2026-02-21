using System.Linq;
using Terra_Namp.Networking;
using Xunit;
using static Terra_Namp.Networking.JukeboxEventMachine;

namespace Terra_Namp.Tests;

/// <summary>
/// Unit tests for JukeboxEventMachine — the pure server-side state machine for boss/death music.
///
/// These tests verify LOGIC correctness. They do NOT verify wall-clock sync between clients,
/// which depends on network latency and is inherently non-deterministic.
///
/// See JukeboxEventMachine.cs header for a detailed explanation of why desync exists in
/// multiplayer and why it cannot be eliminated purely in application logic.
/// </summary>
public class JukeboxEventMachineTests
{
    private static readonly byte[] FakeHash = new byte[] { 1, 2, 3, 4 };
    private static readonly byte[] FakeDeathHash = new byte[] { 5, 6, 7, 8 };

    private static JukeboxConfig BossTrackOnly() =>
        new(FakeHash, null, null, null);

    private static JukeboxConfig BossSoundpadOnly() =>
        new(null, "boss-uuid", null, null);

    private static JukeboxConfig DeathTrackOnly() =>
        new(null, null, FakeDeathHash, null);

    private static JukeboxConfig DeathSoundpadOnly() =>
        new(null, null, null, "death-uuid");

    private static JukeboxConfig NoConfig() =>
        new(null, null, null, null);

    private static GameState NoDeath(bool anyBoss = false) =>
        new(new bool[4], anyBoss);

    private static GameState PlayerDead(int index, bool anyBoss = false)
    {
        var dead = new bool[4];
        dead[index] = true;
        return new GameState(dead, anyBoss);
    }

    private static GameState TwoPlayersDead(bool anyBoss = false)
    {
        var dead = new bool[4];
        dead[0] = true;
        dead[1] = true;
        return new GameState(dead, anyBoss);
    }

    // ────────────────────────────────────────────────────────────────
    // BOSS TRACK
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void BossSpawns_WithBossTrack_EmitsPlayBossTrack()
    {
        var machine = new JukeboxEventMachine(4);

        var events = machine.Tick(new GameState(new bool[4], AnyBossAlive: true), BossTrackOnly());

        Assert.Single(events, e => e.Type == EventType.PlayBossTrack);
    }

    [Fact]
    public void BossAliveSecondTick_DoesNotRepeatPlayBossTrack()
    {
        var machine = new JukeboxEventMachine(4);
        machine.Tick(new GameState(new bool[4], true), BossTrackOnly()); // tick 1: spawn

        var events = machine.Tick(new GameState(new bool[4], true), BossTrackOnly()); // tick 2: still alive

        Assert.Empty(events.Where(e => e.Type == EventType.PlayBossTrack));
    }

    [Fact]
    public void BossDies_WhilePlayingBossTrack_EmitsStopBossTrack()
    {
        var machine = new JukeboxEventMachine(4);
        machine.Tick(new GameState(new bool[4], true), BossTrackOnly()); // spawn → playing

        var events = machine.Tick(new GameState(new bool[4], false), BossTrackOnly()); // dies

        Assert.Single(events, e => e.Type == EventType.StopBossTrack);
    }

    [Fact]
    public void BossDies_NoBossTrackConfigured_NoStopEvent()
    {
        var machine = new JukeboxEventMachine(4);
        machine.Tick(new GameState(new bool[4], true), NoConfig());

        var events = machine.Tick(new GameState(new bool[4], false), NoConfig());

        Assert.Empty(events.Where(e => e.Type == EventType.StopBossTrack));
    }

    [Fact]
    public void BossSpawns_WithBossSoundpad_EmitsPlayBossSoundpad()
    {
        var machine = new JukeboxEventMachine(4);

        var events = machine.Tick(new GameState(new bool[4], true), BossSoundpadOnly());

        Assert.Single(events, e => e.Type == EventType.PlayBossSoundpad);
    }

    [Fact]
    public void BossSpawns_BothTrackAndSoundpad_TrackWins()
    {
        var machine = new JukeboxEventMachine(4);
        var config = new JukeboxConfig(FakeHash, "some-uuid", null, null);

        var events = machine.Tick(new GameState(new bool[4], true), config);

        Assert.Single(events, e => e.Type == EventType.PlayBossTrack);
        Assert.DoesNotContain(events, e => e.Type == EventType.PlayBossSoundpad);
    }

    // ────────────────────────────────────────────────────────────────
    // DEATH TRACK
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void PlayerDies_WithDeathTrack_EmitsPlayDeathTrack()
    {
        var machine = new JukeboxEventMachine(4);

        var events = machine.Tick(PlayerDead(0), DeathTrackOnly());

        Assert.Single(events, e => e.Type == EventType.PlayDeathTrack);
    }

    [Fact]
    public void PlayerDies_WithDeathTrack_SetsTimerTo300()
    {
        var machine = new JukeboxEventMachine(4);

        machine.Tick(PlayerDead(0), DeathTrackOnly());

        Assert.Equal(JukeboxEventMachine.DeathMusicDurationTicks, machine.DeathMusicTimer);
    }

    [Fact]
    public void PlayerDies_WithDeathSoundpad_EmitsPlayDeathSoundpad_NoTimer()
    {
        var machine = new JukeboxEventMachine(4);

        var events = machine.Tick(PlayerDead(0), DeathSoundpadOnly());

        Assert.Single(events, e => e.Type == EventType.PlayDeathSoundpad);
        Assert.Equal(0, machine.DeathMusicTimer); // soundpad manages its own duration
    }

    [Fact]
    public void PlayerStaysDeadNextTick_DoesNotRepeatDeathEvent()
    {
        var machine = new JukeboxEventMachine(4);
        machine.Tick(PlayerDead(0), DeathTrackOnly()); // tick 1: just died

        var events = machine.Tick(PlayerDead(0), DeathTrackOnly()); // tick 2: still dead

        Assert.Empty(events.Where(e => e.Type == EventType.PlayDeathTrack));
    }

    [Fact]
    public void PlayerDies_NoDeathConfig_NoEvent()
    {
        var machine = new JukeboxEventMachine(4);

        var events = machine.Tick(PlayerDead(0), NoConfig());

        Assert.Empty(events);
    }

    // ────────────────────────────────────────────────────────────────
    // CRITICAL: simultaneous player deaths → only ONE packet
    //
    // BUG that existed in PostUpdateWorld before JukeboxEventMachine:
    // The loop iterated all players and sent a PlaySong packet for EACH
    // player who just died. If 2 players died simultaneously, 2 packets
    // were sent → clients received 2 PlaySong back-to-back → track restarted.
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void TwoPlayersDieSimultaneously_OnlyOnePlayDeathTrackEvent()
    {
        var machine = new JukeboxEventMachine(4);

        var events = machine.Tick(TwoPlayersDead(), DeathTrackOnly());

        Assert.Equal(1, events.Count(e => e.Type == EventType.PlayDeathTrack));
    }

    [Fact]
    public void TwoPlayersDieSimultaneously_TimerSetOnce_Not300x2()
    {
        var machine = new JukeboxEventMachine(4);

        machine.Tick(TwoPlayersDead(), DeathTrackOnly());

        Assert.Equal(JukeboxEventMachine.DeathMusicDurationTicks, machine.DeathMusicTimer);
    }

    // ────────────────────────────────────────────────────────────────
    // DEATH TIMER
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void DeathTimer_ExpiresAfterExactly300Ticks_EmitsStopDeathTrack()
    {
        var machine = new JukeboxEventMachine(4);
        machine.Tick(PlayerDead(0), DeathTrackOnly()); // tick 0: death, timer=300

        EventType? lastEvent = null;
        for (int tick = 1; tick <= 300; tick++)
        {
            var events = machine.Tick(NoDeath(), DeathTrackOnly());
            if (events.Any(e => e.Type == EventType.StopDeathTrack))
                lastEvent = EventType.StopDeathTrack;
        }

        Assert.Equal(EventType.StopDeathTrack, lastEvent);
        Assert.Equal(0, machine.DeathMusicTimer);
    }

    [Fact]
    public void DeathTimer_DoesNotStopBefore300Ticks()
    {
        var machine = new JukeboxEventMachine(4);
        machine.Tick(PlayerDead(0), DeathTrackOnly());

        bool stoppedEarly = false;
        for (int tick = 1; tick < 300; tick++) // < not <=
        {
            var events = machine.Tick(NoDeath(), DeathTrackOnly());
            if (events.Any(e => e.Type == EventType.StopDeathTrack))
                stoppedEarly = true;
        }

        Assert.False(stoppedEarly);
    }

    [Fact]
    public void DeathTimer_AfterExpiry_SecondDeathRestartsCycle()
    {
        var machine = new JukeboxEventMachine(4);

        // First death + full timer burn
        machine.Tick(PlayerDead(0), DeathTrackOnly());
        for (int i = 0; i < 300; i++)
            machine.Tick(NoDeath(), DeathTrackOnly());

        // Player respawns
        machine.Tick(NoDeath(), DeathTrackOnly());
        // Player dies again
        var events = machine.Tick(PlayerDead(0), DeathTrackOnly());

        Assert.Single(events, e => e.Type == EventType.PlayDeathTrack);
        Assert.Equal(JukeboxEventMachine.DeathMusicDurationTicks, machine.DeathMusicTimer);
    }

    // ────────────────────────────────────────────────────────────────
    // BOSS + DEATH together
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void PlayerDiesDuringBossFight_BothEventsCanCoexist()
    {
        var config = new JukeboxConfig(FakeHash, null, FakeDeathHash, null);
        var machine = new JukeboxEventMachine(4);

        // Boss spawns
        machine.Tick(new GameState(new bool[4], true), config);

        // Player dies while boss is alive
        var events = machine.Tick(new GameState(new[] { true, false, false, false }, true), config);

        Assert.Single(events, e => e.Type == EventType.PlayDeathTrack);
        Assert.DoesNotContain(events, e => e.Type == EventType.PlayBossTrack); // not re-sent
    }
}
