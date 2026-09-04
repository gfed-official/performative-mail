using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Client.UI;

public readonly record struct PaydaySnapshot(Cents Earned, Cents Quota);
