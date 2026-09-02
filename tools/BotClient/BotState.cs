using PerformativeMail.Sim.Core;

namespace PerformativeMail.BotClient;

public enum BotPhase
{
    Idle,
    Fetching,
    Carrying,
    Delivering,
    Stuck,
}

public readonly record struct BotState(BotPhase Phase, EntityId Target, uint SinceTick);

// 3 m: spec/06-multiplayer.md "Mailbox interact". 12 ticks: 0.4 s hold from spec/03 §3.4 at TickClock.TickHz 30. 900: harness timeout.
public record BotTuning(float ReachMetres = 3f, int HoldTicks = 12, int StuckAfterTicks = 900);
