using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Balance;

public readonly record struct FiveShiftRun(
    ShiftRecord Shift1,
    ShiftRecord Shift2,
    ShiftRecord Shift3,
    ShiftRecord Shift4,
    ShiftRecord Shift5,
    int DurationSeconds)
{
    public int PaydayCount => 5;

    public bool DurationOk => DurationSeconds < BalanceSim.MaxRunSeconds;

    public bool GateHolds => PaydayCount == 5 && DurationOk;

    public ShiftRecord Payday(byte shift) => shift switch
    {
        1 => Shift1,
        2 => Shift2,
        3 => Shift3,
        4 => Shift4,
        5 => Shift5,
        _ => throw new ArgumentOutOfRangeException(nameof(shift), shift, null),
    };

    public static FiveShiftRun Drive(BalanceTable balance)
    {
        if (balance is null) throw new ArgumentNullException(nameof(balance));

        var clock = new ShiftClock(balance, RunState.InLobby());
        if (!clock.TryEnter(RunPhase.Generating) || !clock.TryEnter(RunPhase.Prep))
            throw new InvalidOperationException("FiveShiftRun could not enter Prep.");

        for (uint player = 1; player <= BalanceSim.FourPlayerCount; player++)
            clock.Connect(player);

        var paydays = new ShiftRecord[RunState.ShiftCount];
        for (byte shift = 1; shift <= RunState.ShiftCount; shift++)
        {
            if (clock.State.Phase != RunPhase.Prep || clock.State.Shift != shift)
                throw new InvalidOperationException($"FiveShiftRun expected Prep of shift {shift}.");

            for (uint player = 1; player <= BalanceSim.FourPlayerCount; player++)
                clock.SetReady(player, true);

            if (clock.State.Phase != RunPhase.Delivery)
                throw new InvalidOperationException($"FiveShiftRun Ready did not start Delivery on shift {shift}.");

            clock.AdvanceTo(clock.State.PhaseDeadlineTick);
            if (clock.State.Phase == RunPhase.Raid)
                clock.AdvanceTo(clock.State.PhaseDeadlineTick);
            if (clock.State.Phase != RunPhase.Payday)
                throw new InvalidOperationException($"FiveShiftRun missed Payday on shift {shift}.");

            paydays[shift - 1] = BalanceSim.RunFourPlayer(balance, shift);

            if (clock.State.PhaseDeadlineTick > clock.Now)
                clock.AdvanceTo(clock.State.PhaseDeadlineTick);

            if (!clock.TryEnter(RunPhase.Draft))
                throw new InvalidOperationException($"FiveShiftRun could not enter Draft on shift {shift}.");
            if (!clock.TryAllPicked())
                throw new InvalidOperationException($"FiveShiftRun all-pick failed on shift {shift}.");
        }

        if (clock.State.Phase != RunPhase.Victory)
            throw new InvalidOperationException("FiveShiftRun did not end in Victory.");

        int seconds = (int)(clock.Now / (uint)TickClock.TickHz);
        return new FiveShiftRun(paydays[0], paydays[1], paydays[2], paydays[3], paydays[4], seconds);
    }
}
