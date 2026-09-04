using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Run;

public static class ShiftDurations
{
    public const int RaidWindowSeconds = 90;

    public static int Seconds(RunPhase phase, byte shift, BalanceTable balance)
    {
        if (balance is null) throw new ArgumentNullException(nameof(balance));
        if (shift < 1 || shift > BalanceCatalog.ShiftCount)
            throw new ArgumentOutOfRangeException(nameof(shift), shift, null);

        int i = shift - 1;
        return phase switch
        {
            RunPhase.Prep => balance.PrepSeconds[i],
            RunPhase.Delivery => balance.DeliverySeconds[i],
            RunPhase.Raid => RaidWindowSeconds,
            RunPhase.Payday => balance.PaydaySeconds,
            RunPhase.Draft => balance.DraftSeconds,
            _ => 0,
        };
    }

    public static int Ticks(RunPhase phase, byte shift, BalanceTable balance)
        => TickClock.TicksFromSeconds(Seconds(phase, shift, balance));
}
