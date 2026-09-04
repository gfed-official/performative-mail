using System;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Balance;

public enum DeliveryAgent : byte
{
    HandShift1 = 1,
    HandShift2 = 2,
    Bike = 3,
}

public readonly record struct ShiftRecord(
    byte Shift,
    DeliveryAgent Agent,
    Cents Earnings,
    Cents Quota)
{
    public bool Met => Earnings.Value >= Quota.Value;
}

public static class BalanceSim
{
    public const int HandShift1ValuePerMinute = 220;
    public const int HandShift2ValuePerMinute = 180;
    public const int BikeValuePerMinute = 200;
    public const int FourPlayerCount = 4;
    public const int MaxRunSeconds = 32 * 60;

    public static int ValuePerMinute(DeliveryAgent agent) => agent switch
    {
        DeliveryAgent.HandShift1 => HandShift1ValuePerMinute,
        DeliveryAgent.HandShift2 => HandShift2ValuePerMinute,
        DeliveryAgent.Bike => BikeValuePerMinute,
        _ => throw new ArgumentOutOfRangeException(nameof(agent), agent, null),
    };

    public static DeliveryAgent HandOnly(byte shift) => shift switch
    {
        1 => DeliveryAgent.HandShift1,
        2 => DeliveryAgent.HandShift2,
        _ => throw new ArgumentOutOfRangeException(nameof(shift), shift, null),
    };

    public static ShiftRecord RunHand(BalanceTable balance, byte shift)
    {
        if (balance is null) throw new ArgumentNullException(nameof(balance));

        var agent = HandOnly(shift);
        var budget = QuotaBudget.For(balance, shift, playerCount: 1);
        int seconds = balance.DeliverySeconds[shift - 1];
        int earnings = checked(ValuePerMinute(agent) * seconds / 60);
        return new ShiftRecord(shift, agent, new Cents(earnings), budget.Quota);
    }

    public static bool SoloHandShift1WinShift2Fail(BalanceTable balance)
    {
        var shift1 = RunHand(balance, 1);
        var shift2 = RunHand(balance, 2);
        return shift1.Met && !shift2.Met;
    }

    public static DeliveryAgent FourPlayerAgent(byte shift) => shift switch
    {
        1 => DeliveryAgent.HandShift1,
        2 or 3 or 4 or 5 => DeliveryAgent.Bike,
        _ => throw new ArgumentOutOfRangeException(nameof(shift), shift, null),
    };

    public static ShiftRecord RunFourPlayer(BalanceTable balance, byte shift)
    {
        if (balance is null) throw new ArgumentNullException(nameof(balance));

        var agent = FourPlayerAgent(shift);
        var budget = QuotaBudget.For(balance, shift, FourPlayerCount);
        int seconds = balance.DeliverySeconds[shift - 1];
        int earnings = checked(ValuePerMinute(agent) * FourPlayerCount * seconds / 60);
        return new ShiftRecord(shift, agent, new Cents(earnings), budget.Quota);
    }

    public static string Line(in ShiftRecord record)
    {
        string outcome = record.Met ? "MET" : "MISS";
        return $"shift {record.Shift} hand {record.Earnings.Value} / {record.Quota.Value} {outcome}";
    }

    public static string PaydayLine(in ShiftRecord record)
    {
        string agent = record.Agent == DeliveryAgent.Bike ? "bike" : "hand";
        string outcome = record.Met ? "MET" : "MISS";
        return $"payday {record.Shift} {agent} {record.Earnings.Value} / {record.Quota.Value} {outcome}";
    }

    public static string DurationLine(int seconds) => $"duration {seconds}s";

    public static string HashesLine(int mismatches) => $"hashes {mismatches}";
}
