using System;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Run;

public readonly record struct QuotaBudget
{
    public Cents Quota { get; }

    public Cents SpawnValue { get; }

    private QuotaBudget(Cents quota, Cents spawnValue)
    {
        Quota = quota;
        SpawnValue = spawnValue;
    }

    public static QuotaBudget For(
        BalanceTable balance,
        byte shift,
        int playerCount,
        int playerOffset = 0,
        double quotaMultiplier = 1.0)
    {
        if (balance is null) throw new ArgumentNullException(nameof(balance));
        if (shift < 1 || shift > BalanceCatalog.ShiftCount)
            throw new ArgumentOutOfRangeException(nameof(shift), shift, null);
        if (playerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, null);

        int effective = checked(playerCount + playerOffset);
        if (effective < 1)
            throw new ArgumentOutOfRangeException(nameof(playerOffset), playerOffset, null);
        if (double.IsNaN(quotaMultiplier) || double.IsInfinity(quotaMultiplier) || quotaMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(quotaMultiplier), quotaMultiplier, null);

        int hundredths = PlayerScaleHundredths(effective, balance.PlayerScaleExponent);
        int quotaCents = checked((int)Math.Round(
            balance.BaseQuota[shift - 1] * (hundredths / 100m) * (decimal)quotaMultiplier,
            0,
            MidpointRounding.AwayFromZero));
        int spawnCents = checked((int)Math.Round(
            quotaCents * (decimal)balance.SpawnOverhead,
            0,
            MidpointRounding.AwayFromZero));
        return new QuotaBudget(new Cents(quotaCents), new Cents(spawnCents));
    }

    internal static int PlayerScaleHundredths(int effectivePlayerCount, double exponent)
        => checked((int)Math.Round(Math.Pow(effectivePlayerCount, exponent) * 100.0, MidpointRounding.AwayFromZero));
}
