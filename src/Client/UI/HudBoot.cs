using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public static class HudBoot
{
    public static HudSnapshot Placeholder() =>
        new(RunPhase.Delivery, 1, 0, 2700, new Cents(1820),
            new InteractPrompt.Deliver("13 Larch Lane", "13 Larch Lane"));

    public static HudSnapshot? ForPlayReady() => Placeholder();
}
