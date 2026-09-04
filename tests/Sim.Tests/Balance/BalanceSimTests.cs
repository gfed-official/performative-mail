using System;
using System.IO;
using PerformativeMail.Sim.Balance;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Balance;

public sealed class BalanceSimTests
{
    private static readonly BalanceTable Balance = BalanceCatalog.LoadFile(
        Path.Combine(FindContentRoot(), BalanceCatalog.RelativePath));

    [Fact]
    public void RunHand_Shift1_EarningsMeetQuota()
    {
        var record = BalanceSim.RunHand(Balance, 1);

        Assert.Equal(new Cents(880), record.Earnings);
        Assert.Equal(QuotaBudget.For(Balance, 1, 1).Quota, record.Quota);
        Assert.Equal(new Cents(600), record.Quota);
        Assert.True(record.Earnings.Value >= record.Quota.Value);
        Assert.True(record.Met);
        Assert.Equal(DeliveryAgent.HandShift1, record.Agent);
        Assert.Equal("shift 1 hand 880 / 600 MET", BalanceSim.Line(in record));
    }

    [Fact]
    public void RunHand_Shift2_EarningsMissQuota()
    {
        var record = BalanceSim.RunHand(Balance, 2);

        Assert.Equal(new Cents(810), record.Earnings);
        Assert.Equal(QuotaBudget.For(Balance, 2, 1).Quota, record.Quota);
        Assert.Equal(new Cents(1100), record.Quota);
        Assert.True(record.Earnings.Value < record.Quota.Value);
        Assert.False(record.Met);
        Assert.Equal(DeliveryAgent.HandShift2, record.Agent);
        Assert.Equal("shift 2 hand 810 / 1100 MISS", BalanceSim.Line(in record));
    }

    [Fact]
    public void SoloHandShift1WinShift2Fail_RepoBalance_Holds()
    {
        Assert.True(BalanceSim.SoloHandShift1WinShift2Fail(Balance));
    }

    [Fact]
    public void RunHand_EarningsAreRateTimesDeliveryMinutes()
    {
        var shift1 = BalanceSim.RunHand(Balance, 1);
        var shift2 = BalanceSim.RunHand(Balance, 2);

        Assert.Equal(BalanceSim.HandShift1ValuePerMinute * Balance.DeliverySeconds[0] / 60, shift1.Earnings.Value);
        Assert.Equal(BalanceSim.HandShift2ValuePerMinute * Balance.DeliverySeconds[1] / 60, shift2.Earnings.Value);
    }

    [Fact]
    public void HandOnly_Shift3_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceSim.HandOnly(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceSim.RunHand(Balance, 3));
    }

    [Fact]
    public void RunHand_NullBalance_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BalanceSim.RunHand(null!, 1));
    }

    [Fact]
    public void RunFourPlayer_Shift1_HandMeetsQuota()
    {
        var record = BalanceSim.RunFourPlayer(Balance, 1);

        Assert.Equal(new Cents(3520), record.Earnings);
        Assert.Equal(QuotaBudget.For(Balance, 1, 4).Quota, record.Quota);
        Assert.Equal(new Cents(1476), record.Quota);
        Assert.True(record.Met);
        Assert.Equal(DeliveryAgent.HandShift1, record.Agent);
        Assert.Equal("payday 1 hand 3520 / 1476 MET", BalanceSim.PaydayLine(in record));
    }

    [Fact]
    public void RunFourPlayer_Shift2_BikeMeetsQuota()
    {
        var record = BalanceSim.RunFourPlayer(Balance, 2);

        Assert.Equal(new Cents(3600), record.Earnings);
        Assert.Equal(new Cents(2706), record.Quota);
        Assert.True(record.Met);
        Assert.Equal(DeliveryAgent.Bike, record.Agent);
        Assert.Equal("payday 2 bike 3600 / 2706 MET", BalanceSim.PaydayLine(in record));
    }

    [Fact]
    public void RunFourPlayer_LaterShifts_RecordChapter11BikeRate()
    {
        var shift3 = BalanceSim.RunFourPlayer(Balance, 3);
        var shift4 = BalanceSim.RunFourPlayer(Balance, 4);
        var shift5 = BalanceSim.RunFourPlayer(Balance, 5);

        Assert.Equal(new Cents(3600), shift3.Earnings);
        Assert.Equal(new Cents(4428), shift3.Quota);
        Assert.False(shift3.Met);
        Assert.Equal(new Cents(3600), shift4.Earnings);
        Assert.Equal(new Cents(6642), shift4.Quota);
        Assert.False(shift4.Met);
        Assert.Equal(new Cents(4000), shift5.Earnings);
        Assert.Equal(new Cents(9840), shift5.Quota);
        Assert.False(shift5.Met);
    }

    [Fact]
    public void FiveShiftRun_FivePaydays_DurationUnder32Min()
    {
        var run = FiveShiftRun.Drive(Balance);

        Assert.Equal(5, run.PaydayCount);
        Assert.True(run.DurationOk);
        Assert.True(run.GateHolds);
        Assert.Equal(1450, run.DurationSeconds);
        Assert.True(run.DurationSeconds < BalanceSim.MaxRunSeconds);
        Assert.True(run.Shift1.Met);
        Assert.True(run.Shift2.Met);
        Assert.False(run.Shift3.Met);
        Assert.False(run.Shift4.Met);
        Assert.False(run.Shift5.Met);
        Assert.Equal("duration 1450s", BalanceSim.DurationLine(run.DurationSeconds));
        Assert.True(BalanceSim.SoloHandShift1WinShift2Fail(Balance));
    }

    [Fact]
    public void FourPlayerAgent_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceSim.FourPlayerAgent(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceSim.RunFourPlayer(Balance, 6));
    }

    [Fact]
    public void RunFourPlayer_NullBalance_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BalanceSim.RunFourPlayer(null!, 1));
    }

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, BalanceCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/balance.json");
    }
}
