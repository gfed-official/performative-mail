using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.World;

internal enum BiomeKind : byte
{
    DeepWater = 0,
    ShallowWater = 1,
    Beach = 2,
    Rocky = 3,
    Grassland = 4,
    Forest = 5
}

internal static class BiomeStage
{
    public const int BeachTiles = 3;
    public const int Tan20Num = 3640;
    public const int Tan20Den = 10000;
    public const int DeepCm = -200;

    public static void Classify(
        short[] heights,
        int width,
        int height,
        int tileCm,
        uint forestSeed,
        byte[] biomes)
    {
        int count = width * height;
        var sea = SeaDistance(heights, width, height);
        short maxLand = 0;
        int land = 0;
        for (int i = 0; i < count; i++)
        {
            if (!HeightmapStage.IsLand(heights[i])) continue;
            land++;
            if (heights[i] > maxLand) maxLand = heights[i];
        }

        var grassland = new List<int>();
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                short h = heights[i];
                if (!HeightmapStage.IsLand(h))
                {
                    biomes[i] = h <= DeepCm ? (byte)BiomeKind.DeepWater : (byte)BiomeKind.ShallowWater;
                    continue;
                }

                if (sea[i] > 0 && sea[i] <= BeachTiles)
                {
                    biomes[i] = (byte)BiomeKind.Beach;
                    continue;
                }

                bool rocky = maxLand > 0 && (long)h * 100 >= (long)maxLand * 60;
                if (!rocky)
                    rocky = IsRockySlope(heights, width, height, x, y, tileCm);
                if (rocky)
                {
                    biomes[i] = (byte)BiomeKind.Rocky;
                    continue;
                }

                biomes[i] = (byte)BiomeKind.Grassland;
                grassland.Add(i);
            }
        }

        int forestNeed = (land * 25 + 99) / 100;
        if (forestNeed <= 0 || grassland.Count == 0) return;

        var order = grassland.ToArray();
        Array.Sort(order, (a, b) =>
        {
            int na = NoiseAt(forestSeed, a, width);
            int nb = NoiseAt(forestSeed, b, width);
            int cmp = nb.CompareTo(na);
            if (cmp != 0) return cmp;
            return a.CompareTo(b);
        });

        int take = forestNeed < order.Length ? forestNeed : order.Length;
        for (int i = 0; i < take; i++)
            biomes[order[i]] = (byte)BiomeKind.Forest;
    }

    private static int NoiseAt(uint forestSeed, int index, int width)
    {
        int x = index % width;
        int y = index / width;
        return OpenSimplex2Fixed.Noise2(forestSeed, x << 16, y << 16);
    }

    private static bool IsRockySlope(short[] heights, int width, int height, int x, int y, int tileCm)
    {
        short here = heights[y * width + x];
        if (x > 0 && IsSteep(here, heights[y * width + (x - 1)], tileCm)) return true;
        if (x + 1 < width && IsSteep(here, heights[y * width + (x + 1)], tileCm)) return true;
        if (y > 0 && IsSteep(here, heights[(y - 1) * width + x], tileCm)) return true;
        if (y + 1 < height && IsSteep(here, heights[(y + 1) * width + x], tileCm)) return true;
        return false;
    }

    private static bool IsSteep(short a, short b, int tileCm)
    {
        int dh = a - b;
        if (dh < 0) dh = -dh;
        return (long)dh * Tan20Den > (long)tileCm * Tan20Num;
    }

    private static int[] SeaDistance(short[] heights, int width, int height)
    {
        int count = width * height;
        var dist = new int[count];
        var q = new int[count];
        int head = 0;
        int tail = 0;
        for (int i = 0; i < count; i++)
        {
            if (HeightmapStage.IsLand(heights[i]))
            {
                dist[i] = int.MaxValue;
            }
            else
            {
                dist[i] = 0;
                q[tail++] = i;
            }
        }

        while (head < tail)
        {
            int i = q[head++];
            int x = i % width;
            int y = i / width;
            int nd = dist[i] + 1;
            Relax(x - 1, y);
            Relax(x + 1, y);
            Relax(x, y - 1);
            Relax(x, y + 1);

            void Relax(int nx, int ny)
            {
                if (!WorldGrid.InBounds(nx, ny, width, height)) return;
                int ni = ny * width + nx;
                if (nd >= dist[ni]) return;
                dist[ni] = nd;
                q[tail++] = ni;
            }
        }

        return dist;
    }
}
