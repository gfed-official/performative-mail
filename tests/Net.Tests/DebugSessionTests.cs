using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests;

public sealed class DebugSessionTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(TickClock.TickDurationSeconds);

    [Fact]
    public void MenuInspect_HasNoCheats()
    {
        using var machine = new PlaySessionMachine(new LoopbackStack());
        var snap = machine.Inspect();

        Assert.Equal(DebugConnection.Menu, snap.Connection);
        Assert.False(snap.Host);
        Assert.False(snap.CanCheat);
        Assert.False(machine.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.False(machine.TryAdvancePhase());
        Assert.False(machine.TryResetLocalPawn());
    }

    [Fact]
    public void HostInspect_ShowsLobbyTickSeedAndLocalPlayer()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        var snap = host.Inspect();
        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.Equal(DebugConnection.Playing, snap.Connection);
        Assert.True(snap.Host);
        Assert.True(snap.CanCheat);
        Assert.Equal(play.LocalPlayer.Value, snap.LocalPlayer);
        Assert.Equal(RunPhase.Lobby, snap.Phase);
        Assert.Equal((byte)1, snap.Shift);
        Assert.Equal(RunSettings.Arcade().Seed, snap.Seed);
        Assert.Null(snap.WorldHash);
        Assert.Equal(new Cents(0), snap.Wallet);
        Assert.True(snap.Tick.HasValue);
    }

    [Fact]
    public void HostGiveWallet_CreditsSimWorld()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        Assert.True(host.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.Equal(new Cents(1000), host.Inspect().Wallet);
        Assert.False(host.TryGiveWallet(new Cents(0)));
        Assert.Equal(new Cents(1000), host.Inspect().Wallet);
    }

    [Fact]
    public void HostAdvancePhase_LobbyToGenerating()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        Assert.True(host.TryAdvancePhase());
        Assert.Equal(RunPhase.Generating, host.Inspect().Phase);
        Assert.True(host.TryAdvancePhase());
        Assert.Equal(RunPhase.Prep, host.Inspect().Phase);
    }

    [Fact]
    public void HostResetPawn_ReturnsToSpawn()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        var before = LocalPose(host);
        var forward = new MoveIntent(0, sbyte.MaxValue, 0, InputButtons.None);
        Pump(host, ref now, forward, 30);
        Assert.NotEqual(before, LocalPose(host));

        Assert.True(host.TryResetLocalPawn());
        Pump(host, ref now, MoveIntent.Idle, 4);
        Assert.Equal(PlayerPose.Origin, LocalPose(host));
    }

    [Fact]
    public void GuestCheats_ReturnFalseAndDoNotTouchHostWallet()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 8);

        var guestSnap = guest.Inspect();
        Assert.Equal(DebugConnection.Playing, guestSnap.Connection);
        Assert.False(guestSnap.Host);
        Assert.False(guestSnap.CanCheat);
        Assert.Equal(RunSettings.Arcade().Seed, guestSnap.Seed);
        Assert.Null(guestSnap.Wallet);

        Assert.False(guest.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.False(guest.TryAdvancePhase());
        Assert.False(guest.TryResetLocalPawn());
        Assert.Equal(new Cents(0), host.Inspect().Wallet);
        Assert.Equal(RunPhase.Lobby, host.Inspect().Phase);
    }

    private static PlayerPose LocalPose(PlaySessionMachine machine)
    {
        var play = Assert.IsType<PlaySession.Playing>(machine.State);
        return Assert.Single(play.Pawns, p => p.Role == PawnRole.Local).Pose;
    }

    private static void Pump(PlaySessionMachine machine, ref TimeSpan now, in MoveIntent intent, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            now += Tick;
            machine.Pump(now, in intent);
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
