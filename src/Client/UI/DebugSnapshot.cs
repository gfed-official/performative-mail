using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public enum DebugConnection : byte
{
    Menu = 0,
    Connecting = 1,
    Playing = 2,
    Failed = 3,
}

public readonly record struct DebugSnapshot(
    DebugConnection Connection,
    bool Host,
    uint? LocalPlayer,
    uint? Tick,
    RunPhase? Phase,
    byte? Shift,
    uint? Seed,
    ulong? WorldHash,
    Cents? Wallet,
    bool CanCheat)
{
    public static DebugSnapshot Idle(DebugConnection connection) =>
        new(connection, Host: false, null, null, null, null, null, null, null, CanCheat: false);
}
