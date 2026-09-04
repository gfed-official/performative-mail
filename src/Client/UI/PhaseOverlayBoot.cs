using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public static class PhaseOverlayBoot
{
    public static PaydaySnapshot Payday() =>
        new(new Cents(640), new Cents(2214));

    public static DraftOffer Draft() =>
        new("insured", "quick_hands", "union_rep");

    public static ResultsPayload Results() =>
        ResultsPayload.From(
            true,
            5,
            20,
            10000,
            "small_island",
            0x7F3A9C21,
            new[] { new StampScore("cursed_mail", 1.15), new StampScore("double_raids", 1.25) });
}
