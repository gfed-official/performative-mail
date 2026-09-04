using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Tests.Core;

public sealed class RngStreamTests
{
    [Fact]
    public void NextBounded_SameStream_SameDraws()
    {
        var a = RngStream.Derive(0x7F3A9C21, "towns");
        var b = RngStream.Derive(0x7F3A9C21, "towns");
        Assert.Equal(a.NextBounded(14), b.NextBounded(14));
        Assert.Equal(a.NextBounded(1000), b.NextBounded(1000));
    }

    [Fact]
    public void NextBounded_One_IsZero()
    {
        var stream = RngStream.Derive(1, "addresses");
        Assert.Equal(0u, stream.NextBounded(1));
    }
}
