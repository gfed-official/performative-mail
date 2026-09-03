using System;

namespace PerformativeMail.Sim.Core;

public static class Fnv
{
    public const uint Offset32 = 2166136261;
    public const uint Prime32 = 16777619;
    public const ulong Offset64 = 14695981039346656037UL;
    public const ulong Prime64 = 1099511628211UL;

    public static uint Hash32(ReadOnlySpan<byte> bytes)
    {
        uint hash = Offset32;
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= Prime32;
        }

        return hash;
    }

    public static ulong Hash64(ReadOnlySpan<byte> bytes)
    {
        ulong hash = Offset64;
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= Prime64;
        }

        return hash;
    }

    public static ulong Mix64(ulong hash, byte value) => (hash ^ value) * Prime64;

    public static ulong Mix8(ulong hash, byte value) => Mix64(hash, value);

    public static ulong MixUInt32(ulong hash, uint value)
    {
        hash = Mix64(hash, (byte)value);
        hash = Mix64(hash, (byte)(value >> 8));
        hash = Mix64(hash, (byte)(value >> 16));
        hash = Mix64(hash, (byte)(value >> 24));
        return hash;
    }

    public static ulong MixInt16(ulong hash, short value) => MixUInt16(hash, (ushort)value);

    public static ulong MixUInt16(ulong hash, ushort value)
    {
        hash = Mix64(hash, (byte)value);
        hash = Mix64(hash, (byte)(value >> 8));
        return hash;
    }
}
