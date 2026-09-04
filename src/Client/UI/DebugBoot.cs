using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public static class DebugBoot
{
    public const uint LocalPlayer = 1;
    public const uint Tick = 42;
    public const ulong WorldHash = 0x821670054873680EUL;

    public static DebugSnapshot Placeholder() =>
        new(
            DebugConnection.Playing,
            Host: true,
            LocalPlayer,
            Tick,
            RunPhase.Delivery,
            Shift: 1,
            RunSettings.Arcade().Seed,
            WorldHash,
            new Cents(1820),
            CanCheat: true);
}
