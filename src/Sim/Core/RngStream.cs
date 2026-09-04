using System;
using System.Text;

namespace PerformativeMail.Sim.Core;

public sealed class RngStream
{
    private Pcg32 _rng;

    private RngStream(Pcg32 rng) => _rng = rng;

    public static RngStream Derive(uint seed, string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));

        Span<byte> seedBytes = stackalloc byte[4];
        seedBytes[0] = (byte)seed;
        seedBytes[1] = (byte)(seed >> 8);
        seedBytes[2] = (byte)(seed >> 16);
        seedBytes[3] = (byte)(seed >> 24);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var mix = new byte[seedBytes.Length + nameBytes.Length];
        seedBytes.CopyTo(mix);
        nameBytes.CopyTo(mix.AsSpan(4));

        ulong state = Fnv.Hash64(mix);
        uint seq = Fnv.Hash32(nameBytes);
        return new RngStream(new Pcg32(state, seq));
    }

    public uint NextUInt32() => _rng.NextUInt32();

    public uint NextBounded(uint maxExclusive)
    {
        if (maxExclusive == 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        if (maxExclusive == 1)
            return 0;

        uint threshold = (0u - maxExclusive) % maxExclusive;
        uint r;
        do
        {
            r = _rng.NextUInt32();
        } while (r < threshold);
        return r % maxExclusive;
    }
}
