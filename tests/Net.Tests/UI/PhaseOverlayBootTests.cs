using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class PhaseOverlayBootTests
{
    [Fact]
    public void Payday_IsHudStubEarningsAndQuota()
    {
        var snap = PhaseOverlayBoot.Payday();

        Assert.Equal(640, snap.Earned.Value);
        Assert.Equal(2214, snap.Quota.Value);
    }

    [Fact]
    public void Draft_IsGoldenSeedOfferIds()
    {
        var offer = PhaseOverlayBoot.Draft();

        Assert.Equal(new DraftOffer("insured", "quick_hands", "union_rep"), offer);
    }

    [Fact]
    public void Results_IsChapterVictoryPayload()
    {
        var payload = PhaseOverlayBoot.Results();

        Assert.True(payload.Victory);
        Assert.Equal(14375, payload.Score);
        Assert.Equal("PM1-SMALL-7F3A9C21-CM.DR", payload.SeedString);
    }
}
