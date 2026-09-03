using System;

namespace PerformativeMail.Sim.Run;

public readonly record struct RunState
{
    public const byte ShiftCount = 5;

    public RunState(RunPhase phase, byte shift, uint phaseDeadlineTick)
    {
        if (shift < 1 || shift > ShiftCount)
            throw new ArgumentOutOfRangeException(nameof(shift), shift, null);

        Phase = phase;
        Shift = shift;
        PhaseDeadlineTick = phaseDeadlineTick;
    }

    public RunPhase Phase { get; }

    public byte Shift { get; }

    public uint PhaseDeadlineTick { get; }

    public static RunState InLobby() => new(RunPhase.Lobby, 1, 0);

    public bool TryTransition(RunPhase to, out RunState next)
        => TryTransition(to, PhaseDeadlineTick, out next);

    public bool TryTransition(RunPhase to, uint phaseDeadlineTick, out RunState next)
    {
        if (!RunTransitions.IsLegal(Phase, to, Shift))
        {
            next = this;
            return false;
        }

        next = new RunState(to, NextShift(Phase, to, Shift), phaseDeadlineTick);
        return true;
    }

    private static byte NextShift(RunPhase from, RunPhase to, byte shift)
    {
        if (from == RunPhase.Draft && to == RunPhase.Prep)
            return (byte)(shift + 1);
        if (from == RunPhase.Results && to == RunPhase.Lobby)
            return 1;
        return shift;
    }
}
