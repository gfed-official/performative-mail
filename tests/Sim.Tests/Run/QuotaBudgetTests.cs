using System;
using System.IO;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class QuotaBudgetTests
{
    private static readonly BalanceTable Balance = BalanceCatalog.LoadFile(
        Path.Combine(FindContentRoot(), BalanceCatalog.RelativePath));

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 157)]
    [InlineData(4, 246)]
    [InlineData(8, 386)]
    public void PlayerScaleHundredths_Chapter11Samples(int n, int hundredths)
    {
        Assert.Equal(hundredths, QuotaBudget.PlayerScaleHundredths(n, Balance.PlayerScaleExponent));
    }

    [Fact]
    public void For_Shift1Solo_Quota600AndSpawn960()
    {
        var budget = QuotaBudget.For(Balance, 1, 1);
        Assert.Equal(new Cents(600), budget.Quota);
        Assert.Equal(new Cents(960), budget.SpawnValue);
        Assert.Equal(MailSpawnConstants.Shift1SpawnValueCents, budget.SpawnValue.Value);
    }

    [Fact]
    public void For_Shift1FourPlayers_Quota1476()
    {
        Assert.Equal(new Cents(1476), QuotaBudget.For(Balance, 1, 4).Quota);
    }

    [Fact]
    public void For_Shift1EightPlayers_Quota2316()
    {
        Assert.Equal(new Cents(2316), QuotaBudget.For(Balance, 1, 8).Quota);
    }

    [Theory]
    [InlineData(1, 4, 1476)]
    [InlineData(1, 8, 2316)]
    [InlineData(2, 4, 2706)]
    [InlineData(2, 8, 4246)]
    [InlineData(3, 4, 4428)]
    [InlineData(3, 8, 6948)]
    [InlineData(4, 4, 6642)]
    [InlineData(4, 8, 10422)]
    [InlineData(5, 4, 9840)]
    [InlineData(5, 8, 15440)]
    public void For_Chapter11Quotas(byte shift, int playerCount, int quotaCents)
    {
        Assert.Equal(new Cents(quotaCents), QuotaBudget.For(Balance, shift, playerCount).Quota);
    }

    [Theory]
    [InlineData(1, 960)]
    [InlineData(2, 1760)]
    [InlineData(3, 2880)]
    [InlineData(4, 4320)]
    [InlineData(5, 6400)]
    public void For_Solo_SpawnValue(byte shift, int spawnCents)
    {
        Assert.Equal(new Cents(spawnCents), QuotaBudget.For(Balance, shift, 1).SpawnValue);
    }

    [Fact]
    public void For_PlayerCountZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QuotaBudget.For(Balance, 1, 0));
    }

    [Fact]
    public void For_ShiftOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QuotaBudget.For(Balance, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuotaBudget.For(Balance, 6, 1));
    }

    [Fact]
    public void For_NullBalance_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QuotaBudget.For(null!, 1, 1));
    }

    [Fact]
    public void For_PlayerOffsetTwo_SoloShift1_UsesScaleOfThree()
    {
        Assert.Equal(204, QuotaBudget.PlayerScaleHundredths(3, Balance.PlayerScaleExponent));
        Assert.Equal(new Cents(1224), QuotaBudget.For(Balance, 1, 1, playerOffset: 2).Quota);
    }

    [Fact]
    public void For_QuotaMultiplierNineTenths_SoloShift1_Is540()
    {
        var budget = QuotaBudget.For(Balance, 1, 1, quotaMultiplier: 0.9);
        Assert.Equal(new Cents(540), budget.Quota);
        Assert.Equal(new Cents(864), budget.SpawnValue);
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
