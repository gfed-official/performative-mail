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

    [Fact]
    public void MixUInt32_LittleEndian_MatchesByteHash()
    {
        ulong mixed = Fnv.MixUInt32(Fnv.Offset64, 0xA1B2C3D4);
        ulong hashed = Fnv.Hash64(new byte[] { 0xD4, 0xC3, 0xB2, 0xA1 });
        Assert.Equal(hashed, mixed);
    }
}
