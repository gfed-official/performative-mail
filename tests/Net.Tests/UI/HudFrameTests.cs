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
        Assert.Equal("HP 100", frame.HpLabel);
        Assert.Equal(100, frame.HpPct);
        Assert.Equal("Wt 0", frame.WeightLabel);
        Assert.Equal(0, frame.WeightPoints);
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
            23,
            100,
            0);
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
    public void SameDisplay_IgnoresNowInsideTheSameSecond()
    {
        var a = Snapshot(InteractPrompt.None.Instance, now: 1, deadline: 2700);
        var b = Snapshot(InteractPrompt.None.Instance, now: 29, deadline: 2700);

        Assert.True(HudFrame.SameDisplay(in a, in b));
        Assert.Equal(HudFrame.From(in a).TimerLabel, HudFrame.From(in b).TimerLabel);
    }

    [Fact]
    public void SameDisplay_FalseWhenTimerSecondChanges()
    {
        var a = Snapshot(InteractPrompt.None.Instance, now: 0, deadline: 2700);
        var b = Snapshot(InteractPrompt.None.Instance, now: 30, deadline: 2700);

        Assert.False(HudFrame.SameDisplay(in a, in b));
        Assert.NotEqual(HudFrame.From(in a).TimerLabel, HudFrame.From(in b).TimerLabel);
    }

    [Fact]
    public void From_HpAndWeight_FormatsReplicaFields()
    {
        var snap = Snapshot(InteractPrompt.None.Instance, hpPct: 64, weightPoints: 12);
        var frame = HudFrame.From(in snap);

        Assert.Equal("HP 64", frame.HpLabel);
        Assert.Equal(64, frame.HpPct);
        Assert.Equal("Wt 12", frame.WeightLabel);
        Assert.Equal(12, frame.WeightPoints);
    }

    [Fact]
    public void SameDisplay_FalseWhenHpOrWeightChanges()
    {
        var none = Snapshot(InteractPrompt.None.Instance);
        var hurt = Snapshot(InteractPrompt.None.Instance, hpPct: 40);
        var laden = Snapshot(InteractPrompt.None.Instance, weightPoints: 8);

        Assert.False(HudFrame.SameDisplay(in none, in hurt));
        Assert.False(HudFrame.SameDisplay(in none, in laden));
        Assert.True(HudFrame.SameDisplay(in none, Snapshot(InteractPrompt.None.Instance)));
    }

    [Fact]
    public void SameDisplay_FalseWhenWalletOrInteractChanges()
    {
        var none = Snapshot(InteractPrompt.None.Instance);
        var pickup = Snapshot(new InteractPrompt.Pickup(Held));
        var wallet = new HudSnapshot(
            RunPhase.Delivery, 1, 0, 2700, new Cents(1),
            InteractPrompt.None.Instance, new Cents(640), new Cents(2214), 23, 100, 0);

        Assert.True(HudFrame.SameDisplay(in none, Snapshot(InteractPrompt.None.Instance)));
        Assert.False(HudFrame.SameDisplay(in none, in pickup));
        Assert.False(HudFrame.SameDisplay(in none, in wallet));
    }

    [Fact]
    public void From_Shift_FormatsNOver5()
    {
        var frame = HudFrame.From(Snapshot(InteractPrompt.None.Instance, shift: 3));
        Assert.Equal("Shift 3 / 5", frame.ShiftLabel);
    }

    [Fact]
    public void From_PrepReplica_IsNotPlaceholderDelivery()
    {
        var live = new HudSnapshot(
            RunPhase.Prep,
            1,
            240,
            1800,
            new Cents(1820),
            InteractPrompt.None.Instance,
            new Cents(1820),
            new Cents(640),
            0,
            100,
            0);
        var frame = HudFrame.From(in live);
        var placeholder = HudFrame.From(HudBoot.Placeholder());

        Assert.Equal("PREP", frame.PhaseLabel);
        Assert.Equal("Shift 1 / 5", frame.ShiftLabel);
        Assert.Equal("00:52", frame.TimerLabel);
        Assert.Equal("", frame.HeldAddress);
        Assert.Equal("", frame.TargetAddress);
        Assert.Equal("", frame.MatchLabel);
        Assert.Equal("0", frame.ComplaintLabel);
        Assert.NotEqual(placeholder.PhaseLabel, frame.PhaseLabel);
        Assert.NotEqual(placeholder.TimerLabel, frame.TimerLabel);
        Assert.NotEqual(placeholder.HeldAddress, frame.HeldAddress);
        Assert.NotEqual(placeholder.ComplaintLabel, frame.ComplaintLabel);
        Assert.Equal("DELIVERY", placeholder.PhaseLabel);
        Assert.Equal("01:30", placeholder.TimerLabel);
        Assert.Equal("13 Larch Lane", placeholder.HeldAddress);
    }

    private static HudSnapshot Snapshot(
        InteractPrompt interact,
        uint now = 0,
        uint deadline = 2700,
        byte shift = 1,
        int earnings = 640,
        int quota = 2214,
        int complaint = 23,
        byte hpPct = 100,
        int weightPoints = 0) =>
        new(RunPhase.Delivery, shift, now, deadline, new Cents(1820), interact,
            new Cents(earnings), new Cents(quota), complaint, hpPct, weightPoints);
}
