using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Tests.Core;

public sealed class Pcg32Tests
{
    [Fact]
    public void Constructor_OfficialDemoSeed_FirstDraw()
    {
        var rng = new Pcg32(42, 54);
        Assert.Equal(0xA15C02B7u, rng.NextUInt32());
    }

    [Fact]
    public void NextUInt32_SameSeed_SameSequence()
    {
        var a = new Pcg32(42, 54);
        var b = new Pcg32(42, 54);
        Assert.Equal(a.NextUInt32(), b.NextUInt32());
        Assert.Equal(a.NextUInt32(), b.NextUInt32());
    }
}
