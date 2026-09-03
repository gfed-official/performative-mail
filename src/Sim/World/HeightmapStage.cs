using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

internal readonly struct HeightmapResult
{
    public HeightmapResult(short[] heights, bool[] buildable, bool valid, int attempts)
    {
        Heights = heights;
        Buildable = buildable;
        Valid = valid;
        Attempts = attempts;
    }

    public short[] Heights { get; }

    public bool[] Buildable { get; }

    public bool Valid { get; }

    public int Attempts { get; }
}

internal static class HeightmapStage
{
    public const int SeaLevelCm = 0;
    public const int MaxAttempts = 8;
    public const int StartRadiusTiles = 118;
    public const int RadiusStepTiles = 10;
    public const int PeakCm = 2200;
    public const int Octaves = 4;
    public const int BaseFrequencyQ16 = 1024;
    public const int LandLiftQ16 = OpenSimplex2Fixed.One / 5;
    public const int MinBuildablePermille = 300;
    public const int Tan35Num = 7002;
    public const int Tan35Den = 10000;

    public static HeightmapResult Generate(RngStream stream, int width, int height, int tileCm) =>
        Generate(stream, width, height, tileCm, quota: null);

    internal static HeightmapResult Generate(
        RngStream stream,
        int width,
        int height,
        int tileCm,
        Func<short[], bool[], bool>? quota)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));

        long noiseSeed = stream.NextUInt32();
        int count = width * height;
        var heights = new short[count];
        var buildable = new bool[count];
        Func<short[], bool[], bool> accept = quota ?? MeetsBuildableQuota;
        int radius = StartRadiusTiles;
        int attempts = 0;
        bool valid = false;

        for (int i = 0; i < MaxAttempts; i++)
        {
            attempts++;
            Fill(heights, noiseSeed, width, height, radius);
            SmoothCoastline(heights, width, height);
            FlagBuildable(heights, buildable, width, height, tileCm);
            if (accept(heights, buildable))
            {
                valid = true;
                break;
            }

            radius += RadiusStepTiles;
        }

        return new HeightmapResult(heights, buildable, valid, attempts);
    }

    internal static void Fill(short[] heights, long noiseSeed, int width, int height, int radiusTiles)
    {
        int originX = width / 2;
        int originY = height / 2;
        long radiusSq = (long)radiusTiles * radiusTiles;
        if (radiusSq <= 0) radiusSq = 1;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            int dy = y - originY;
            for (int x = 0; x < width; x++)
            {
                int dx = x - originX;
                int falloff = RadialFalloff(dx, dy, radiusSq);
                int noise = Fbm(noiseSeed, x, y);
                int raised = (noise + OpenSimplex2Fixed.One) >> 1;
                int shaped = OpenSimplex2Fixed.Mul(raised + LandLiftQ16, falloff) - (OpenSimplex2Fixed.One - falloff);
                int cm = (int)(((long)shaped * PeakCm) >> 16);
                if (cm > short.MaxValue) cm = short.MaxValue;
                if (cm < short.MinValue) cm = short.MinValue;
                heights[row + x] = (short)cm;
            }
        }
    }

    internal static void SmoothCoastline(short[] heights, int width, int height)
    {
        var doomed = new int[width * height];
        int guard = 0;
        int maxPasses = width * height;
        bool removed;
        do
        {
            int n = 0;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int i = row + x;
                    if (!IsLand(heights[i])) continue;
                    if (LandNeighbors(heights, width, height, x, y) < 2)
                        doomed[n++] = i;
                }
            }

            removed = n > 0;
            for (int k = 0; k < n; k++)
                heights[doomed[k]] = SeaLevelCm;
        } while (removed && ++guard < maxPasses);
    }

    internal static void FlagBuildable(short[] heights, bool[] buildable, int width, int height, int tileCm)
    {
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                buildable[i] = IsLand(heights[i]) && !IsCliff(heights, width, height, x, y, tileCm);
            }
        }
    }

    internal static bool MeetsBuildableQuota(short[] heights, bool[] buildable)
    {
        int land = 0;
        int flat = 0;
        for (int i = 0; i < heights.Length; i++)
        {
            if (!IsLand(heights[i])) continue;
            land++;
            if (buildable[i]) flat++;
        }

        if (land == 0) return false;
        return (long)flat * 1000 >= (long)land * MinBuildablePermille;
    }

    internal static bool IsLand(short heightCm) => heightCm > SeaLevelCm;

    internal static int LandNeighbors(short[] heights, int width, int height, int x, int y)
    {
        int n = 0;
        if (x > 0 && IsLand(heights[y * width + (x - 1)])) n++;
        if (x + 1 < width && IsLand(heights[y * width + (x + 1)])) n++;
        if (y > 0 && IsLand(heights[(y - 1) * width + x])) n++;
        if (y + 1 < height && IsLand(heights[(y + 1) * width + x])) n++;
        return n;
    }

    private static bool IsCliff(short[] heights, int width, int height, int x, int y, int tileCm)
    {
        short here = heights[y * width + x];
        if (x > 0 && IsCliffPair(here, heights[y * width + (x - 1)], tileCm)) return true;
        if (x + 1 < width && IsCliffPair(here, heights[y * width + (x + 1)], tileCm)) return true;
        if (y > 0 && IsCliffPair(here, heights[(y - 1) * width + x], tileCm)) return true;
        if (y + 1 < height && IsCliffPair(here, heights[(y + 1) * width + x], tileCm)) return true;
        return false;
    }

    private static bool IsCliffPair(short a, short b, int tileCm)
    {
        int dh = a - b;
        if (dh < 0) dh = -dh;
        return (long)dh * Tan35Den > (long)tileCm * Tan35Num;
    }

    private static int Fbm(long seed, int tileX, int tileY)
    {
        int sum = 0;
        int amp = OpenSimplex2Fixed.One;
        int freq = BaseFrequencyQ16;
        int ampSum = 0;
        for (int o = 0; o < Octaves; o++)
        {
            int xq = (int)((long)tileX * freq);
            int yq = (int)((long)tileY * freq);
            sum += OpenSimplex2Fixed.Mul(OpenSimplex2Fixed.Noise2(seed + o * 0x9E3779B97F4A7C15L, xq, yq), amp);
            ampSum += amp;
            amp >>= 1;
            freq <<= 1;
        }

        return (int)(((long)sum << 16) / ampSum);
    }

    private static int RadialFalloff(int dx, int dy, long radiusSq)
    {
        long distSq = (long)dx * dx + (long)dy * dy;
        if (distSq >= radiusSq) return 0;
        int t = (int)((distSq << 16) / radiusSq);
        int oneMinus = OpenSimplex2Fixed.One - t;
        return OpenSimplex2Fixed.Mul(oneMinus, oneMinus);
    }
}
