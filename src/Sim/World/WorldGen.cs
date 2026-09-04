using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class WorldGen
{
    public const int SmallIslandTiles = 300;
    public const int SmallIslandTileCm = 200;

    public static WorldTables GenerateSmallIsland(uint seed)
    {
        var stream = RngStream.Derive(seed, "heightmap");
        var result = HeightmapStage.Generate(stream, SmallIslandTiles, SmallIslandTiles, SmallIslandTileCm);
        return new WorldTables(
            SmallIslandTiles,
            SmallIslandTiles,
            SmallIslandTileCm,
            result.Heights,
            Array.Empty<AddressId>(),
            result.Buildable,
            result.Valid,
            result.Attempts);
    }
}
