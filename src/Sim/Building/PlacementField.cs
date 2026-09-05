using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Building;

public sealed class PlacementField
{
    public const int Tan15Num = 2679;
    public const int Tan15Den = 10000;
    public const int MaxFlattenCm = 100;
    private const int FlattenBoundLo = int.MinValue / 4;
    private const int FlattenBoundHi = int.MaxValue / 4;

    private readonly short[] _heights;
    private readonly bool[] _street;

    public PlacementField(int width, int height, int tileCm, short[] heights, bool[] street)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (street is null) throw new ArgumentNullException(nameof(street));
        if (heights.Length != width * height)
            throw new ArgumentException("Height buffer must be width × height.", nameof(heights));
        if (street.Length != heights.Length)
            throw new ArgumentException("Street buffer must match height buffer.", nameof(street));

        Width = width;
        Height = height;
        TileCm = tileCm;
        _heights = (short[])heights.Clone();
        _street = (bool[])street.Clone();
    }

    public int Width { get; }

    public int Height { get; }

    public int TileCm { get; }

    public static PlacementField Flat(int width, int height, int tileCm, short heightCm = 100)
    {
        var heights = new short[width * height];
        for (int i = 0; i < heights.Length; i++)
            heights[i] = heightCm;
        return new PlacementField(width, height, tileCm, heights, new bool[heights.Length]);
    }

    public static PlacementField FromWorld(WorldTables tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        var street = new bool[tables.Width * tables.Height];
        var streets = tables.Streets;
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null) continue;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (!InBounds(tile, tables.Width, tables.Height)) continue;
                street[Idx(tile, tables.Width)] = true;
            }
        }

        return new PlacementField(tables.Width, tables.Height, tables.TileCm, tables.Heights, street);
    }

    public PlacementField WithStreet(TileCoord tile)
    {
        RequireInBounds(tile);
        var street = (bool[])_street.Clone();
        street[Idx(tile, Width)] = true;
        return new PlacementField(Width, Height, TileCm, _heights, street);
    }

    public PlacementField WithHeight(TileCoord tile, short heightCm)
    {
        RequireInBounds(tile);
        var heights = (short[])_heights.Clone();
        heights[Idx(tile, Width)] = heightCm;
        return new PlacementField(Width, Height, TileCm, heights, _street);
    }

    public bool InBounds(TileCoord tile) => InBounds(tile, Width, Height);

    public bool IsStreet(TileCoord tile) => InBounds(tile) && _street[Idx(tile, Width)];

    public bool IsWater(TileCoord tile) =>
        InBounds(tile) && _heights[Idx(tile, Width)] <= HeightmapStage.SeaLevelCm;

    public short HeightAt(TileCoord tile)
    {
        RequireInBounds(tile);
        return _heights[Idx(tile, Width)];
    }

    private int MaxLegalSlopeCm() => TileCm * Tan15Num / Tan15Den;

    public bool TryPlanFlatten(IReadOnlyList<TileCoord> tiles, out FlattenedTile[] planned)
    {
        planned = Array.Empty<FlattenedTile>();
        if (tiles is null) throw new ArgumentNullException(nameof(tiles));
        if (tiles.Count == 0) return true;

        int maxLegalDh = MaxLegalSlopeCm();
        int lo = FlattenBoundLo;
        int hi = FlattenBoundHi;
        long sum = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (!InBounds(tile)) return false;
            int h = _heights[Idx(tile, Width)];
            sum += h;
            lo = Math.Max(lo, h - MaxFlattenCm);
            hi = Math.Min(hi, h + MaxFlattenCm);
        }

        lo = Math.Max(lo, HeightmapStage.SeaLevelCm + 1);
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            TightenByNeighbor(tile.X - 1, tile.Y, tiles, maxLegalDh, ref lo, ref hi);
            TightenByNeighbor(tile.X + 1, tile.Y, tiles, maxLegalDh, ref lo, ref hi);
            TightenByNeighbor(tile.X, tile.Y - 1, tiles, maxLegalDh, ref lo, ref hi);
            TightenByNeighbor(tile.X, tile.Y + 1, tiles, maxLegalDh, ref lo, ref hi);
        }

        if (lo > hi) return false;

        int prefer = (int)(sum / tiles.Count);
        int target = prefer < lo ? lo : prefer > hi ? hi : prefer;
        var changed = new List<FlattenedTile>(tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            short h = _heights[Idx(tile, Width)];
            if (h == target) continue;
            changed.Add(new FlattenedTile(tile.X, tile.Y, target));
        }

        planned = changed.Count == 0 ? Array.Empty<FlattenedTile>() : changed.ToArray();
        return true;
    }

    public void ApplyFlatten(IReadOnlyList<FlattenedTile> planned)
    {
        if (planned is null) throw new ArgumentNullException(nameof(planned));
        for (int i = 0; i < planned.Count; i++)
        {
            var delta = planned[i];
            if (delta.H < short.MinValue || delta.H > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(planned), "Height must fit in a short.");
            var tile = new TileCoord(delta.X, delta.Y);
            RequireInBounds(tile);
            _heights[Idx(tile, Width)] = (short)delta.H;
        }
    }

    public bool SlopeExceeds(TileCoord tile)
    {
        if (!InBounds(tile)) return true;
        short here = _heights[Idx(tile, Width)];
        if (Steep(here, tile.X - 1, tile.Y)) return true;
        if (Steep(here, tile.X + 1, tile.Y)) return true;
        if (Steep(here, tile.X, tile.Y - 1)) return true;
        if (Steep(here, tile.X, tile.Y + 1)) return true;
        return false;
    }

    private bool Steep(short here, int x, int y)
    {
        if (!InBounds(new TileCoord(x, y), Width, Height)) return false;
        int dh = here - _heights[Idx(new TileCoord(x, y), Width)];
        if (dh < 0) dh = -dh;
        return (long)dh * Tan15Den > (long)TileCm * Tan15Num;
    }

    private void TightenByNeighbor(
        int x,
        int y,
        IReadOnlyList<TileCoord> footprint,
        int maxLegalDh,
        ref int lo,
        ref int hi)
    {
        var neighbor = new TileCoord(x, y);
        if (!InBounds(neighbor, Width, Height) || InFootprint(footprint, neighbor))
            return;
        int hn = _heights[Idx(neighbor, Width)];
        lo = Math.Max(lo, hn - maxLegalDh);
        hi = Math.Min(hi, hn + maxLegalDh);
    }

    private static bool InFootprint(IReadOnlyList<TileCoord> tiles, TileCoord tile)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == tile) return true;
        }

        return false;
    }

    private void RequireInBounds(TileCoord tile)
    {
        if (!InBounds(tile))
            throw new ArgumentOutOfRangeException(nameof(tile), tile, null);
    }

    private static bool InBounds(TileCoord tile, int width, int height) =>
        (uint)tile.X < (uint)width && (uint)tile.Y < (uint)height;

    private static int Idx(TileCoord tile, int width) => tile.Y * width + tile.X;
}
