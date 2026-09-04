using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class ResultsFrameTests
{
    [Fact]
    public void From_Boot_PrintsScoreAndSeed()
    {
        var frame = ResultsFrame.From(PhaseOverlayBoot.Results());

        Assert.Equal("14375", frame.ScoreLabel);
        Assert.Equal("PM1-SMALL-7F3A9C21-CM.DR", frame.SeedLabel);
    }

    [Fact]
    public void From_EmptyStamps_PrintsPayloadSeed()
    {
        var payload = ResultsPayload.From(
            false,
            1,
            0,
            1820,
            "small_island",
            0x7F3A9C21,
            Array.Empty<StampScore>());
        var frame = ResultsFrame.From(in payload);

        Assert.Equal("1820", frame.ScoreLabel);
        Assert.Equal("PM1-SMALL-7F3A9C21", frame.SeedLabel);
    }
}
