using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class WorldHash
{
    public static ulong Compute(WorldTables tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));

        ulong hash = Fnv.Offset64;
        hash = Fnv.MixUInt32(hash, (uint)tables.Width);
        hash = Fnv.MixUInt32(hash, (uint)tables.Height);
        hash = Fnv.MixUInt32(hash, (uint)tables.TileCm);
        var heights = tables.Heights;
        for (int i = 0; i < heights.Length; i++)
            hash = Fnv.MixInt16(hash, heights[i]);
        hash = Fnv.MixUInt32(hash, (uint)tables.Addresses.Length);
        var addresses = tables.Addresses;
        for (int i = 0; i < addresses.Length; i++)
            hash = Fnv.MixUInt32(hash, addresses[i].Packed);
        return hash;
    }
}
