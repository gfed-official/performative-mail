using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public readonly record struct HudSnapshot(
    RunPhase Phase,
    byte Shift,
    uint Now,
    uint Deadline,
    Cents Wallet,
    InteractPrompt Interact);
