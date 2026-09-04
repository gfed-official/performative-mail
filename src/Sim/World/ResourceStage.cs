using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

internal static class ResourceStage
{
    public const int WoodMin = 6;
    public const int FiberMin = 1;
    public const int StoneMin = 3;
    public const int IronOreMin = 8;
    public const int SandMin = 1;
    public const int BerriesMin = 1;

    public static ResourceNodeRecord[] Place(
        RngStream resources,
        short[] heights,
        bool[] occupied,
        PostOfficeRecord po,
        int width,
        int height,
        int tileCm)
    {
        if (resources is null) throw new ArgumentNullException(nameof(resources));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (occupied is null) throw new ArgumentNullException(nameof(occupied));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));

        int count = width * height;
        uint forestSeed = resources.NextUInt32();
        var biomes = new byte[count];
        BiomeStage.Classify(heights, width, height, tileCm, forestSeed, biomes);

        int pcx = po.Tile.X + po.SizeTiles.X / 2;
        int pcy = po.Tile.Y + po.SizeTiles.Y / 2;
        var used = new bool[count];
        var nodes = new List<ResourceNodeRecord>();

        PlaceKind(ResourceKind.Wood, WoodMin, b => b == (byte)BiomeKind.Forest);
        PlaceKind(ResourceKind.Fiber, FiberMin, b => b == (byte)BiomeKind.Forest || b == (byte)BiomeKind.Grassland);
        PlaceKind(ResourceKind.Stone, StoneMin, b => b == (byte)BiomeKind.Rocky);
        PlaceKind(ResourceKind.IronOre, IronOreMin, b => b == (byte)BiomeKind.Rocky);
        PlaceKind(ResourceKind.Sand, SandMin, b => b == (byte)BiomeKind.Beach);
        PlaceKind(ResourceKind.Berries, BerriesMin, b => b == (byte)BiomeKind.Grassland);

        return nodes.ToArray();

        void PlaceKind(ResourceKind kind, int need, Func<byte, bool> ok)
        {
            var candidates = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (!ok(biomes[i])) continue;
                if (occupied[i] || used[i]) continue;
                if (!HeightmapStage.IsLand(heights[i])) continue;
                candidates.Add(i);
            }

            candidates.Sort((a, b) =>
            {
                int da = WorldGrid.DistSq(a % width, a / width, pcx, pcy);
                int db = WorldGrid.DistSq(b % width, b / width, pcx, pcy);
                int cmp = db.CompareTo(da);
                if (cmp != 0) return cmp;
                int ya = a / width;
                int yb = b / width;
                cmp = ya.CompareTo(yb);
                if (cmp != 0) return cmp;
                return (a % width).CompareTo(b % width);
            });

            int take = need < candidates.Count ? need : candidates.Count;
            for (int i = 0; i < take; i++)
            {
                int idx = candidates[i];
                used[idx] = true;
                nodes.Add(new ResourceNodeRecord(kind, new TileCoord(idx % width, idx / width)));
            }
        }
    }
}
