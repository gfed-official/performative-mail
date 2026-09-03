using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class WorldGen
{
    public const int SmallIslandTiles = 300;
    public const int SmallIslandTileCm = 200;

    public static WorldTables GenerateSmallIsland(uint seed)
    {
        int count = SmallIslandTiles * SmallIslandTiles;
        var heights = new short[count];
        var stream = RngStream.Derive(seed, "heightmap");
        for (int y = 0; y < SmallIslandTiles; y++)
        {
            int row = y * SmallIslandTiles;
            for (int x = 0; x < SmallIslandTiles; x++)
            {
                uint u = stream.NextUInt32();
                heights[row + x] = (short)((int)(u % 5001u) - 1000);
            }
        }

        return new WorldTables(
            SmallIslandTiles,
            SmallIslandTiles,
            SmallIslandTileCm,
            heights,
            Array.Empty<AddressId>());
    }
}
