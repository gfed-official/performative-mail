using System;

namespace PerformativeMail.Sim.Run;

public static class PostalRankXp
{
    public const int PerShift = 100;
    public const int VictoryBonus = 50;
    public const int PerDelivery = 5;

    public static int Award(int shiftsCompleted, bool victory, int deliveries)
    {
        if (shiftsCompleted < 0 || shiftsCompleted > RunState.ShiftCount)
            throw new ArgumentOutOfRangeException(nameof(shiftsCompleted), shiftsCompleted, null);
        if (victory && shiftsCompleted != RunState.ShiftCount)
            throw new ArgumentOutOfRangeException(nameof(shiftsCompleted), shiftsCompleted, null);
        if (deliveries < 0)
            throw new ArgumentOutOfRangeException(nameof(deliveries), deliveries, null);

        int victoryXp = victory ? VictoryBonus : 0;
        return checked(PerShift * shiftsCompleted + victoryXp + PerDelivery * deliveries);
    }
}
