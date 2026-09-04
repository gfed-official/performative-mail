using System;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Building;

public sealed class PlacementField
{
    public const int Tan15Num = 2679;
    public const int Tan15Den = 10000;

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

    public bool IsWater(TileCoord tile) => InBounds(tile) && _heights[Idx(tile, Width)] <= 0;

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

    private void RequireInBounds(TileCoord tile)
    {
        if (!InBounds(tile))
            throw new ArgumentOutOfRangeException(nameof(tile), tile, null);
    }

    private static bool InBounds(TileCoord tile, int width, int height) =>
        (uint)tile.X < (uint)width && (uint)tile.Y < (uint)height;

    private static int Idx(TileCoord tile, int width) => tile.Y * width + tile.X;
}
