using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class DebugFrameTests
{
    [Fact]
    public void From_Placeholder_ShowsPlayingHostInspectAndCheats()
    {
        var frame = DebugFrame.From(DebugBoot.Placeholder());

        Assert.Equal("PLAYING", frame.ConnectionLabel);
        Assert.Equal(DebugFrame.HostRole, frame.RoleLabel);
        Assert.Equal("42", frame.TickLabel);
        Assert.Equal("DELIVERY", frame.PhaseLabel);
        Assert.Equal("1", frame.ShiftLabel);
        Assert.Equal("0x7F3A9C21", frame.SeedLabel);
        Assert.Equal("0x821670054873680E", frame.WorldHashLabel);
        Assert.Equal("1", frame.PlayerLabel);
        Assert.Equal("$18.20", frame.WalletLabel);
        Assert.Equal(DebugFrame.HostAuthority, frame.AuthorityLabel);
        Assert.True(frame.CanCheat);
    }

    [Fact]
    public void From_Menu_HidesCheatsAndFillsMissing()
    {
        var frame = DebugFrame.From(DebugSnapshot.Idle(DebugConnection.Menu));

        Assert.Equal("MENU", frame.ConnectionLabel);
        Assert.Equal(DebugFrame.Missing, frame.RoleLabel);
        Assert.Equal(DebugFrame.Missing, frame.TickLabel);
        Assert.Equal(DebugFrame.Missing, frame.PhaseLabel);
        Assert.Equal(DebugFrame.Missing, frame.SeedLabel);
        Assert.Equal(DebugFrame.Missing, frame.WorldHashLabel);
        Assert.Equal(DebugFrame.Missing, frame.PlayerLabel);
        Assert.Equal(DebugFrame.Missing, frame.WalletLabel);
        Assert.Equal(DebugFrame.InspectAuthority, frame.AuthorityLabel);
        Assert.False(frame.CanCheat);
    }

    [Fact]
    public void From_PlayingGuest_IsInspectOnly()
    {
        var snap = new DebugSnapshot(
            DebugConnection.Playing,
            Host: false,
            LocalPlayer: 2,
            Tick: 9,
            RunPhase.Lobby,
            Shift: 1,
            RunSettings.Arcade().Seed,
            WorldHash: null,
            Wallet: null,
            CanCheat: false);

        var frame = DebugFrame.From(in snap);

        Assert.Equal("PLAYING", frame.ConnectionLabel);
        Assert.Equal(DebugFrame.GuestRole, frame.RoleLabel);
        Assert.Equal("9", frame.TickLabel);
        Assert.Equal("LOBBY", frame.PhaseLabel);
        Assert.Equal(DebugFrame.Missing, frame.WorldHashLabel);
        Assert.Equal(DebugFrame.Missing, frame.WalletLabel);
        Assert.Equal(DebugFrame.InspectAuthority, frame.AuthorityLabel);
        Assert.False(frame.CanCheat);
    }

    [Theory]
    [InlineData(0, "$0.00")]
    [InlineData(1000, "$10.00")]
    [InlineData(-4, "-$0.04")]
    public void From_Wallet_FormatsCents(int cents, string expected)
    {
        var snap = new DebugSnapshot(
            DebugConnection.Playing,
            Host: true,
            LocalPlayer: 1,
            Tick: 0,
            RunPhase.Lobby,
            Shift: 1,
            Seed: 1,
            WorldHash: null,
            new Cents(cents),
            CanCheat: true);

        Assert.Equal(expected, DebugFrame.From(in snap).WalletLabel);
        Assert.Equal(1000, DebugFrame.WalletGrantCents);
    }
}
