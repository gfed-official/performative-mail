using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class JoinTargetTests
{
    [Fact]
    public void TryParse_HostOnly_UsesDefaultPort()
    {
        Assert.True(JoinTarget.TryParse("192.168.1.20", 7777, out var target));
        Assert.Equal("192.168.1.20", target.Host);
        Assert.Equal((ushort)7777, target.Port);
    }

    [Fact]
    public void TryParse_HostAndPort()
    {
        Assert.True(JoinTarget.TryParse("10.0.0.2:24567", 7777, out var target));
        Assert.Equal("10.0.0.2", target.Host);
        Assert.Equal((ushort)24567, target.Port);
    }

    [Fact]
    public void TryParse_RejectsPortZero()
    {
        Assert.False(JoinTarget.TryParse("127.0.0.1:0", 7777, out _));
    }

    [Fact]
    public void FailReason_Unreachable_PrintsAddress()
    {
        var reason = new FailReason.Unreachable(new JoinTarget("192.168.1.20", 7777));
        Assert.Contains("192.168.1.20:7777", reason.Message());
    }

    [Fact]
    public void FailReason_Rejected_VersionMismatch_NamesWorldHash()
    {
        var reason = new FailReason.Rejected(HelloRejectReason.VersionMismatch);
        Assert.Contains("Version mismatch", reason.Message());
        Assert.Contains("World hash", reason.Message());
    }
}

public sealed class ViewFrameTests
{
    [Fact]
    public void From_MapsSimAxesToYUpMetres()
    {
        var east = ViewFrame.From(new PlayerPose(100, 0, 0, 0));
        Assert.Equal(1f, east.X);
        Assert.Equal(0f, east.Y);
        Assert.Equal(0f, east.Z);

        var north = ViewFrame.From(new PlayerPose(0, 100, 0, 0));
        Assert.Equal(0f, north.X);
        Assert.Equal(0f, north.Y);
        Assert.Equal(-1f, north.Z);

        var up = ViewFrame.From(new PlayerPose(0, 0, 200, 0));
        Assert.Equal(2f, up.Y);
    }

    [Fact]
    public void From_NegatesClockwiseYaw()
    {
        var pose = new PlayerPose(0, 0, 0, 16384);
        var view = ViewFrame.From(in pose);
        Assert.Equal((float)(-Math.PI / 2.0), view.YawRadians, 5);
    }
}

public sealed class RenderClockTests
{
    [Fact]
    public void Anchor_ThenNow_AddsWallRemainder()
    {
        var clock = new RenderClock();
        var wall = TimeSpan.FromSeconds(2);
        clock.Anchor(30, wall);

        Assert.True(clock.TryNow(wall, out var atAnchor));
        Assert.Equal(InterpolationBuffer.TimeOfTick(30), atAnchor);

        Assert.True(clock.TryNow(wall + TimeSpan.FromMilliseconds(40), out var later));
        Assert.Equal(InterpolationBuffer.TimeOfTick(30) + TimeSpan.FromMilliseconds(40), later);
    }

    [Fact]
    public void Anchor_SameTick_DoesNotMove()
    {
        var clock = new RenderClock();
        clock.Anchor(3, TimeSpan.FromSeconds(1));
        clock.Anchor(3, TimeSpan.FromSeconds(2));
        Assert.True(clock.TryNow(TimeSpan.FromSeconds(2), out var now));
        Assert.Equal(InterpolationBuffer.TimeOfTick(3) + TimeSpan.FromSeconds(1), now);
    }
}

public sealed class CombinedServerLinkTests
{
    [Fact]
    public void Poll_MergesLocalThenRemote_AndSendRoutesBySeat()
    {
        var hostPair = new LoopbackTransport();
        var guestPair = new LoopbackTransport();
        var local = LoopbackLink.OverPipes(hostPair.A);
        var remote = LoopbackLink.OverPipes(new[] { guestPair.A }, firstId: 1);
        var combined = new CombinedServerLink(local, remote);

        Assert.True(combined.TryPoll(out var hostOpened));
        Assert.Equal(ConnectionId.HostSeat, hostOpened.Connection);
        Assert.True(combined.TryPoll(out var guestOpened));
        Assert.Equal(new ConnectionId(1), guestOpened.Connection);

        hostPair.B.Send(0, new byte[] { 1 });
        guestPair.B.Send(0, new byte[] { 2 });
        Assert.True(combined.TryPoll(out var hostData));
        Assert.Equal(new byte[] { 1 }, hostData.Payload);
        Assert.True(combined.TryPoll(out var guestData));
        Assert.Equal(new byte[] { 2 }, guestData.Payload);

        combined.Send(ConnectionId.HostSeat, 1, new byte[] { 9 });
        combined.Send(new ConnectionId(1), 1, new byte[] { 8 });
        Assert.True(hostPair.B.Poll(out _, out var toHost));
        Assert.Equal(new byte[] { 9 }, toHost);
        Assert.True(guestPair.B.Poll(out _, out var toGuest));
        Assert.Equal(new byte[] { 8 }, toGuest);
    }
}

public sealed class PlaySessionTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(TickClock.TickDurationSeconds);

    [Fact]
    public void HostAndGuest_ReachPlaying_AndSeeTwoDistinctPawns()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;

        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 8);

        var hostPlay = Assert.IsType<PlaySession.Playing>(host.State);
        var guestPlay = Assert.IsType<PlaySession.Playing>(guest.State);
        Assert.IsType<SessionRole.Listening>(hostPlay.Role);
        Assert.IsType<SessionRole.Guest>(guestPlay.Role);
        Assert.NotEqual(hostPlay.LocalPlayer, guestPlay.LocalPlayer);
        Assert.Equal(2, guestPlay.Pawns.Count);
        Assert.Single(guestPlay.Pawns, p => p.Role == PawnRole.Local);
        Assert.Single(guestPlay.Pawns, p => p.Role == PawnRole.Remote);
        Assert.NotEqual(guestPlay.Pawns[0].Pose, guestPlay.Pawns[1].Pose);
    }

    [Fact]
    public void HostWalks_GuestSeesRemoteMove()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 8);

        var guestBefore = Assert.IsType<PlaySession.Playing>(guest.State);
        var remoteBefore = Assert.Single(guestBefore.Pawns, p => p.Role == PawnRole.Remote).Pose;
        var forward = new MoveIntent(0, sbyte.MaxValue, 0, InputButtons.None);
        PumpBoth(host, guest, ref now, forward, 30);

        var guestAfter = Assert.IsType<PlaySession.Playing>(guest.State);
        var remoteAfter = Assert.Single(guestAfter.Pawns, p => p.Role == PawnRole.Remote).Pose;
        Assert.NotEqual(remoteBefore, remoteAfter);
    }

    [Fact]
    public void GuestLeave_HoldsSeatOnHost()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 8);
        Assert.Equal(2, Assert.IsType<PlaySession.Playing>(host.State).Pawns.Count);

        guest.Leave();
        Assert.IsType<PlaySession.Menu>(guest.State);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 4);

        Assert.Equal(2, Assert.IsType<PlaySession.Playing>(host.State).Pawns.Count);
    }

    [Fact]
    public void HostLeave_FailsGuestWithHostLost()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 8);

        host.Leave();
        now += Tick;
        guest.Pump(now, MoveIntent.Idle);

        var failed = Assert.IsType<PlaySession.Failed>(guest.State);
        Assert.IsType<FailReason.HostLost>(failed.Reason);
    }

    [Fact]
    public void SoloHost_ClockPause_FreezesMovement()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        PumpHost(host, ref now, MoveIntent.Idle, 8);

        var started = Assert.IsType<PlaySession.Playing>(host.State).Pawns[0].Pose;
        var forward = new MoveIntent(0, sbyte.MaxValue, 0, InputButtons.None);
        PumpHost(host, ref now, forward, 15);
        var moved = Assert.IsType<PlaySession.Playing>(host.State).Pawns[0].Pose;
        Assert.NotEqual(started, moved);

        Assert.True(host.TrySetClockPaused(true));
        Assert.True(host.ClockPaused);
        PumpHost(host, ref now, forward, 15);
        Assert.Equal(moved, Assert.IsType<PlaySession.Playing>(host.State).Pawns[0].Pose);

        Assert.True(host.TrySetClockPaused(false));
        Assert.False(host.ClockPaused);
        PumpHost(host, ref now, forward, 15);
        Assert.NotEqual(moved, Assert.IsType<PlaySession.Playing>(host.State).Pawns[0].Pose);
    }

    [Fact]
    public void TwoPlayers_ClockPause_RejectedAndRunContinues()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 8);

        Assert.False(host.TrySetClockPaused(true));
        Assert.False(host.ClockPaused);
        Assert.False(guest.TrySetClockPaused(true));
        Assert.False(guest.ClockPaused);

        var guestBefore = Assert.IsType<PlaySession.Playing>(guest.State);
        var remoteBefore = Assert.Single(guestBefore.Pawns, p => p.Role == PawnRole.Remote).Pose;
        var forward = new MoveIntent(0, sbyte.MaxValue, 0, InputButtons.None);
        PumpBoth(host, guest, ref now, forward, 30);
        var remoteAfter = Assert.Single(
            Assert.IsType<PlaySession.Playing>(guest.State).Pawns,
            p => p.Role == PawnRole.Remote).Pose;
        Assert.NotEqual(remoteBefore, remoteAfter);
    }

    [Fact]
    public void SecondPlayerJoin_ClearsSoloClockPause()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        PumpHost(host, ref now, MoveIntent.Idle, 8);
        Assert.True(host.TrySetClockPaused(true));

        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 8);
        Assert.False(host.ClockPaused);
        Assert.IsType<PlaySession.Playing>(guest.State);
    }

    [Fact]
    public void SecondHost_FailsPortInUse()
    {
        var stack = new LoopbackStack();
        using var a = new PlaySessionMachine(stack);
        using var b = new PlaySessionMachine(stack);
        a.Host();
        b.Host();

        var failed = Assert.IsType<PlaySession.Failed>(b.State);
        var port = Assert.IsType<FailReason.PortInUse>(failed.Reason);
        Assert.Equal(SessionOptions.DefaultPort, port.Port);
        Assert.Contains("7777", failed.Reason.Message());
    }

    [Fact]
    public void JoinWithNobodyListening_IsUnreachable()
    {
        var stack = new LoopbackStack();
        using var guest = new PlaySessionMachine(stack);
        guest.Join(stack.LocalTarget);
        guest.Pump(TimeSpan.FromSeconds(1), MoveIntent.Idle);

        var failed = Assert.IsType<PlaySession.Failed>(guest.State);
        Assert.IsType<FailReason.Unreachable>(failed.Reason);
        Assert.Contains("127.0.0.1:7777", failed.Reason.Message());
    }

    private static void PumpHost(
        PlaySessionMachine host,
        ref TimeSpan now,
        in MoveIntent intent,
        int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            now += Tick;
            host.Pump(now, in intent);
        }
    }

    private static void PumpBoth(
        PlaySessionMachine host,
        PlaySessionMachine guest,
        ref TimeSpan now,
        in MoveIntent intent,
        int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            now += Tick;
            host.Pump(now, in intent);
            guest.Pump(now, in intent);
        }
    }
}
