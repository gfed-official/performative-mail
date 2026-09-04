using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public readonly record struct MailboxPose(int XCm, int YCm, int ZCm, int YawDegrees)
{
    public bool OnLattice(int latticeCm)
    {
        if (latticeCm <= 0) return false;
        return XCm % latticeCm == 0 && YCm % latticeCm == 0 && ZCm % latticeCm == 0;
    }

    public TileCoord Tile(int tileCm)
    {
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));
        return new TileCoord(FloorDiv(XCm, tileCm), FloorDiv(YCm, tileCm));
    }

    private static int FloorDiv(int value, int divisor)
    {
        int q = value / divisor;
        if ((value < 0) != (divisor < 0) && value % divisor != 0) q--;
        return q;
    }
}

public readonly record struct HouseRecord(
    AddressId Address,
    TileCoord LotTile,
    TileCoord LotSizeTiles,
    MailboxPose Mailbox)
{
    public TileRect Lot => new(LotTile.X, LotTile.Y, LotSizeTiles.X, LotSizeTiles.Y);
}

public readonly record struct PostOfficeRecord(
    TileCoord Tile,
    TileCoord SizeTiles,
    TileCoord SpawnPadTile,
    TileCoord IntakeTile,
    Facing IntakeFace)
{
    public TileRect Footprint => new(Tile.X, Tile.Y, SizeTiles.X, SizeTiles.Y);
}

public readonly record struct StreetRecord(byte Id, string Name, byte District, TileCoord[] Tiles);

public readonly record struct LotRecord(
    int Id,
    byte StreetId,
    byte District,
    TileRect Footprint,
    bool Residential);

public enum ResourceKind : byte
{
    Wood = 1,
    Fiber = 2,
    Stone = 3,
    IronOre = 4,
    Sand = 5,
    Berries = 6
}

public readonly record struct ResourceNodeRecord(ResourceKind Kind, TileCoord Tile);

public readonly record struct FerryLaneRecord(TileCoord A, TileCoord B);

public readonly record struct RouteNodeRecord(int Id, TileCoord Tile);

public readonly record struct RouteEdgeRecord(int From, int To, int LengthTiles, byte Surface);

public readonly record struct SpawnEdgeRecord(byte District, TileCoord Tile, TileCoord[] PathToPo);
