using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class DraftFrameTests
{
    [Fact]
    public void From_Boot_PrintsThreeCardIds()
    {
        var frame = DraftFrame.From(PhaseOverlayBoot.Draft());

        Assert.Equal("insured", frame.Card1Label);
        Assert.Equal("quick_hands", frame.Card2Label);
        Assert.Equal("union_rep", frame.Card3Label);
    }

    [Fact]
    public void From_Offer_UsesPayloadIds()
    {
        var frame = DraftFrame.From(new DraftOffer("long_legs", "union_rep", "insured"));

        Assert.Equal("long_legs", frame.Card1Label);
        Assert.Equal("union_rep", frame.Card2Label);
        Assert.Equal("insured", frame.Card3Label);
    }
}
