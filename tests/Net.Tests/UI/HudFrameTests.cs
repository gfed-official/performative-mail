using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class HudFrameTests
{
    private const string Held = "13 Larch Lane";
    private const string Other = "8 Oak Street";

    [Fact]
    public void From_Pickup_ShowsMailAddressWithoutMatch()
    {
        var frame = HudFrame.From(Snapshot(new InteractPrompt.Pickup(Held)));

        Assert.Equal(Held, frame.HeldAddress);
        Assert.Equal("", frame.TargetAddress);
        Assert.Equal(MatchMark.None, frame.Match);
        Assert.Equal("", frame.MatchLabel);
    }

    [Fact]
    public void From_DeliverMatch_ShiftDeliveryTimerWalletAndTick()
    {
        var frame = HudFrame.From(Snapshot(
            new InteractPrompt.Deliver(Held, Held)));

        Assert.Equal("Shift 1 / 5", frame.ShiftLabel);
        Assert.Equal("DELIVERY", frame.PhaseLabel);
        Assert.Equal("01:30", frame.TimerLabel);
        Assert.Equal(TimerTone.Normal, frame.TimerTone);
        Assert.Equal("$18.20", frame.WalletLabel);
        Assert.Equal(Held, frame.HeldAddress);
        Assert.Equal(Held, frame.TargetAddress);
        Assert.Equal(MatchMark.Tick, frame.Match);
        Assert.Equal("tick", frame.MatchLabel);
        Assert.Equal("Quota 640 / 2214", frame.QuotaLabel);
        Assert.Equal("", frame.SurplusLabel);
        Assert.Equal("23", frame.ComplaintLabel);
        Assert.False(frame.QuotaMet);
    }

    [Fact]
    public void From_DeliverMismatch_ShowsBothAddressesAndCross()
    {
        var frame = HudFrame.From(Snapshot(
            new InteractPrompt.Deliver(Held, Other)));

        Assert.Equal(Held, frame.HeldAddress);
        Assert.Equal(Other, frame.TargetAddress);
        Assert.Equal(MatchMark.Cross, frame.Match);
        Assert.Equal("cross", frame.MatchLabel);
    }

    [Fact]
    public void From_None_ClearsInteract()
    {
        var frame = HudFrame.From(Snapshot(InteractPrompt.None.Instance));

        Assert.Equal("", frame.HeldAddress);
        Assert.Equal("", frame.TargetAddress);
        Assert.Equal(MatchMark.None, frame.Match);
        Assert.Equal("", frame.MatchLabel);
    }

    [Theory]
    [InlineData(0u, 2700u, "01:30")]
    [InlineData(0u, 30u, "00:01")]
    [InlineData(0u, 29u, "00:00")]
    [InlineData(2700u, 2700u, "00:00")]
    [InlineData(2701u, 2700u, "00:00")]
    public void From_Timer_UsesTickHz30(uint now, uint deadline, string timer)
    {
        var frame = HudFrame.From(Snapshot(InteractPrompt.None.Instance, now, deadline));
        Assert.Equal(timer, frame.TimerLabel);
        Assert.Equal(TickClock.TickHz, 30);
    }

    [Theory]
    [InlineData(0u, 1830u, TimerTone.Normal)]
    [InlineData(0u, 1800u, TimerTone.Amber)]
    [InlineData(0u, 480u, TimerTone.Amber)]
    [InlineData(0u, 450u, TimerTone.Red)]
    [InlineData(0u, 0u, TimerTone.Red)]
    public void From_TimerTone_AmberAt60sRedAt15s(uint now, uint deadline, TimerTone tone)
    {
        var frame = HudFrame.From(Snapshot(InteractPrompt.None.Instance, now, deadline));
        Assert.Equal(tone, frame.TimerTone);
    }

    [Theory]
    [InlineData(0, "$0.00")]
    [InlineData(1820, "$18.20")]
    [InlineData(8, "$0.08")]
    [InlineData(-4, "-$0.04")]
    [InlineData(-500, "-$5.00")]
    public void From_Wallet_FormatsCents(int cents, string expected)
    {
        var snap = new HudSnapshot(
            RunPhase.Delivery,
            1,
            0,
            2700,
            new Cents(cents),
            InteractPrompt.None.Instance,
            new Cents(640),
            new Cents(2214),
            23);
        Assert.Equal(expected, HudFrame.From(in snap).WalletLabel);
    }

    [Fact]
    public void From_QuotaMet_ShowsSurplus()
    {
        var frame = HudFrame.From(Snapshot(InteractPrompt.None.Instance, earnings: 2214, quota: 2214));

        Assert.Equal("Quota 2214 / 2214", frame.QuotaLabel);
        Assert.Equal(HudFrame.SurplusText, frame.SurplusLabel);
        Assert.True(frame.QuotaMet);
        Assert.Equal(2214, frame.QuotaEarnings);
        Assert.Equal(2214, frame.QuotaTarget);
    }

    [Fact]
    public void From_QuotaOver_ShowsSurplus()
    {
        var frame = HudFrame.From(Snapshot(InteractPrompt.None.Instance, earnings: 2215, quota: 2214));

        Assert.Equal("Quota 2215 / 2214", frame.QuotaLabel);
        Assert.Equal("+surplus", frame.SurplusLabel);
        Assert.True(frame.QuotaMet);
        Assert.Equal(2215, frame.QuotaEarnings);
        Assert.Equal(2214, frame.QuotaTarget);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(100, "100")]
    public void From_ComplaintPoints(int points, string expected)
    {
        var frame = HudFrame.From(Snapshot(InteractPrompt.None.Instance, complaint: points));
        Assert.Equal(expected, frame.ComplaintLabel);
    }

    [Fact]
    public void From_Shift_FormatsNOver5()
    {
        var frame = HudFrame.From(Snapshot(InteractPrompt.None.Instance, shift: 3));
        Assert.Equal("Shift 3 / 5", frame.ShiftLabel);
    }

    private static HudSnapshot Snapshot(
        InteractPrompt interact,
        uint now = 0,
        uint deadline = 2700,
        byte shift = 1,
        int earnings = 640,
        int quota = 2214,
        int complaint = 23) =>
        new(RunPhase.Delivery, shift, now, deadline, new Cents(1820), interact,
            new Cents(earnings), new Cents(quota), complaint);
}
