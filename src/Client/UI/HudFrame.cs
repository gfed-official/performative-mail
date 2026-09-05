using System;
using System.Globalization;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public readonly record struct HudFrame(
    string ShiftLabel,
    string PhaseLabel,
    string TimerLabel,
    TimerTone TimerTone,
    string WalletLabel,
    string HeldAddress,
    string TargetAddress,
    MatchMark Match,
    string QuotaLabel,
    string SurplusLabel,
    string ComplaintLabel,
    bool QuotaMet,
    int QuotaEarnings,
    int QuotaTarget)
{
    public const int ShiftCount = 5;
    public const int AmberSeconds = 60;
    public const int RedSeconds = 15;
    public const string TickText = "tick";
    public const string CrossText = "cross";
    public const string SurplusText = "+surplus";

    public string MatchLabel => Match switch
    {
        MatchMark.Tick => TickText,
        MatchMark.Cross => CrossText,
        MatchMark.None => "",
        _ => throw new ArgumentOutOfRangeException(nameof(Match), Match, null),
    };

    public static bool SameDisplay(in HudSnapshot a, in HudSnapshot b) =>
        a.Phase == b.Phase
        && a.Shift == b.Shift
        && a.Wallet == b.Wallet
        && a.Earnings == b.Earnings
        && a.Quota == b.Quota
        && a.Complaint == b.Complaint
        && a.Deadline == b.Deadline
        && RemainingSeconds(in a) == RemainingSeconds(in b)
        && Equals(a.Interact, b.Interact);

    public static HudFrame From(in HudSnapshot snapshot)
    {
        int remainingSeconds = RemainingSeconds(in snapshot);

        int earnings = snapshot.Earnings.Value;
        int quota = snapshot.Quota.Value;
        bool quotaMet = earnings >= quota;

        var (held, target, mark) = Prompt(snapshot.Interact);
        return new HudFrame(
            $"Shift {snapshot.Shift} / {ShiftCount}",
            PhaseName(snapshot.Phase),
            $"{remainingSeconds / 60:D2}:{remainingSeconds % 60:D2}",
            Tone(remainingSeconds),
            FormatWallet(snapshot.Wallet),
            held,
            target,
            mark,
            $"Quota {earnings} / {quota}",
            quotaMet ? SurplusText : "",
            snapshot.Complaint.ToString(CultureInfo.InvariantCulture),
            quotaMet,
            earnings,
            quota);
    }

    internal static int RemainingSeconds(in HudSnapshot snapshot)
    {
        uint remainingTicks = snapshot.Deadline > snapshot.Now
            ? snapshot.Deadline - snapshot.Now
            : 0;
        return (int)(remainingTicks / TickClock.TickHz);
    }

    private static (string Held, string Target, MatchMark Mark) Prompt(InteractPrompt interact)
    {
        switch (interact)
        {
            case InteractPrompt.None:
                return ("", "", MatchMark.None);
            case InteractPrompt.Pickup pickup:
                return (pickup.Address ?? "", "", MatchMark.None);
            case InteractPrompt.Deliver deliver:
                bool match = string.Equals(deliver.HeldAddress, deliver.TargetAddress, StringComparison.Ordinal);
                return (deliver.HeldAddress ?? "", deliver.TargetAddress ?? "", match ? MatchMark.Tick : MatchMark.Cross);
            default:
                throw new ArgumentOutOfRangeException(nameof(interact), interact, null);
        }
    }

    private static string PhaseName(RunPhase phase) => phase switch
    {
        RunPhase.Lobby => "LOBBY",
        RunPhase.Generating => "GENERATING",
        RunPhase.Prep => "PREP",
        RunPhase.Delivery => "DELIVERY",
        RunPhase.Raid => "RAID",
        RunPhase.Payday => "PAYDAY",
        RunPhase.Draft => "DRAFT",
        RunPhase.Results => "RESULTS",
        RunPhase.RunOver => "RUN OVER",
        RunPhase.Victory => "VICTORY",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    private static TimerTone Tone(int remainingSeconds)
    {
        if (remainingSeconds <= RedSeconds)
            return TimerTone.Red;
        if (remainingSeconds <= AmberSeconds)
            return TimerTone.Amber;
        return TimerTone.Normal;
    }

    private static string FormatWallet(Cents wallet)
    {
        int value = wallet.Value;
        int abs = value < 0 ? -value : value;
        string amount = $"${abs / 100}.{abs % 100:D2}";
        return value < 0 ? "-" + amount : amount;
    }
}
