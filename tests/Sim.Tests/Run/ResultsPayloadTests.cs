using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class ResultsPayloadTests
{
    private static readonly StampScore CursedMail = new("cursed_mail", 1.15);
    private static readonly StampScore DoubleRaids = new("double_raids", 1.25);

    [Fact]
    public void From_VictoryChapterSeed_PinsPayload()
    {
        var payload = ResultsPayload.From(
            true,
            5,
            20,
            10000,
            "small_island",
            0x7F3A9C21,
            new[] { CursedMail, DoubleRaids });

        Assert.True(payload.Victory);
        Assert.Equal(5, payload.ShiftsCompleted);
        Assert.Equal(20, payload.Deliveries);
        Assert.Equal(650, payload.PostalRankXp);
        Assert.Equal(14375, payload.Score);
        Assert.Equal("PM1-SMALL-7F3A9C21-CM.DR", payload.SeedString);
        Assert.Equal(new[] { CursedMail, DoubleRaids }, payload.Stamps);
    }

    [Fact]
    public void From_StampProduct_RoundsAwayFromZero()
    {
        var payload = ResultsPayload.From(
            false,
            2,
            0,
            10000,
            "small_island",
            0x7F3A9C21,
            new[] { CursedMail, DoubleRaids });

        Assert.Equal(14375, payload.Score);
        Assert.Equal(200, payload.PostalRankXp);
    }

    [Fact]
    public void From_EmptyStamps_ScoreEqualsEarnings()
    {
        var payload = ResultsPayload.From(
            false,
            1,
            0,
            1820,
            "small_island",
            0x7F3A9C21,
            Array.Empty<StampScore>());

        Assert.Equal(1820, payload.Score);
        Assert.Equal("PM1-SMALL-7F3A9C21", payload.SeedString);
        Assert.Empty(payload.Stamps);
    }

    [Fact]
    public void From_PreservesStampOrder()
    {
        var payload = ResultsPayload.From(
            true,
            5,
            0,
            0,
            "small_island",
            1,
            new[] { DoubleRaids, CursedMail });

        Assert.Equal(new[] { DoubleRaids, CursedMail }, payload.Stamps);
        Assert.Equal("PM1-SMALL-00000001-DR.CM", payload.SeedString);
    }

    [Fact]
    public void StampScore_BadId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new StampScore("CursedMail", 1.15));
        Assert.Throws<ArgumentException>(() => new StampScore("", 1.15));
    }

    [Fact]
    public void StampScore_NonPositiveMult_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StampScore("cursed_mail", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StampScore("cursed_mail", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StampScore("cursed_mail", double.NaN));
    }

    [Fact]
    public void From_NullStamps_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ResultsPayload.From(
            false,
            1,
            0,
            0,
            "small_island",
            0,
            null!));
    }
}
