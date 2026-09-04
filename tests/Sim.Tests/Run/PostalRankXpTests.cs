using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class PostalRankXpTests
{
    [Theory]
    [InlineData(5, true, 0)]
    [InlineData(5, true, 20)]
    [InlineData(1, false, 0)]
    [InlineData(2, false, 7)]
    [InlineData(0, false, 3)]
    [InlineData(5, false, 0)]
    [InlineData(4, false, 11)]
    public void Award_PinsUnitTableFormula(int shifts, bool victory, int deliveries)
    {
        Assert.Equal(
            100 * shifts + 50 * (victory ? 1 : 0) + 5 * deliveries,
            PostalRankXp.Award(shifts, victory, deliveries));
    }

    [Fact]
    public void Award_Victory_FiveShifts_ZeroDeliveries_Is550()
    {
        Assert.Equal(550, PostalRankXp.Award(5, true, 0));
    }

    [Fact]
    public void Award_Victory_FiveShifts_TwentyDeliveries_Is650()
    {
        Assert.Equal(650, PostalRankXp.Award(5, true, 20));
    }

    [Fact]
    public void Award_RunOver_OneShift_Is100()
    {
        Assert.Equal(100, PostalRankXp.Award(1, false, 0));
    }

    [Fact]
    public void Award_RunOver_TwoShifts_SevenDeliveries_Is235()
    {
        Assert.Equal(235, PostalRankXp.Award(2, false, 7));
    }

    [Fact]
    public void Award_RunOver_ZeroShifts_ThreeDeliveries_Is15()
    {
        Assert.Equal(15, PostalRankXp.Award(0, false, 3));
    }

    [Fact]
    public void Award_NegativeDeliveries_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PostalRankXp.Award(1, false, -1));
    }

    [Fact]
    public void Award_ShiftsCompletedSix_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PostalRankXp.Award(6, false, 0));
    }

    [Fact]
    public void Award_VictoryWithFourShifts_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PostalRankXp.Award(4, true, 0));
    }
}
