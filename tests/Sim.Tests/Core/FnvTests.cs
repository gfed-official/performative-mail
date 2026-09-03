using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Tests.Core;

public sealed class FnvTests
{
    [Fact]
    public void Hash64_Empty_IsOffset()
    {
        Assert.Equal(Fnv.Offset64, Fnv.Hash64(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Hash32_Empty_IsOffset()
    {
        Assert.Equal(Fnv.Offset32, Fnv.Hash32(ReadOnlySpan<byte>.Empty));
    }
}
