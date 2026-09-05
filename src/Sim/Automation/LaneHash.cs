using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Automation;

public static class LaneHash
{
    public const int QuantumCm = 25;

    public static int Quantize(int positionCm) => positionCm / QuantumCm;

    public static uint Of(IReadOnlyList<int> positionsHeadFirst)
    {
        if (positionsHeadFirst is null)
            return Fnv.Offset32;

        uint hash = Fnv.Offset32;
        for (int i = 0; i < positionsHeadFirst.Count; i++)
            hash = Mix32(hash, unchecked((uint)Quantize(positionsHeadFirst[i])));

        return hash;
    }

    private static uint Mix32(uint hash, uint value)
    {
        hash ^= (byte)value;
        hash *= Fnv.Prime32;
        hash ^= (byte)(value >> 8);
        hash *= Fnv.Prime32;
        hash ^= (byte)(value >> 16);
        hash *= Fnv.Prime32;
        hash ^= (byte)(value >> 24);
        hash *= Fnv.Prime32;
        return hash;
    }
}
