using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class DebugWorld
{
    public const int Width = 16;
    public const int Height = 12;
    public const int TileCm = 200;
    public const string StreetName = "Debug Lane";
    public const short LandHeightCm = 100;
    public const ulong Hash = 0x4CF184F2FA4D4EEEUL;

    public static readonly TileCoord PostOfficeTile = new(0, 0);
    public static readonly TileCoord PostOfficeSize = new(6, 6);
    public static readonly TileCoord SpawnPadTile = new(2, 2);
    public static readonly TileCoord IntakeTile = new(5, 2);
    public static readonly TileCoord House1Lot = new(0, 8);
    public static readonly TileCoord House2Lot = new(8, 8);
    public static readonly TileCoord LotSize = new(4, 4);
    public static readonly MailboxPose House1Mailbox = new(200, 1200, 0, 180);
    public static readonly MailboxPose House2Mailbox = new(1800, 1200, 0, 180);

    public static WorldTables Tables()
    {
        int count = Width * Height;
        var heights = new short[count];
        var buildable = new bool[count];
        for (int i = 0; i < count; i++)
        {
            heights[i] = LandHeightCm;
            buildable[i] = true;
        }

        var streetTiles = new TileCoord[Width];
        for (int x = 0; x < Width; x++)
            streetTiles[x] = new TileCoord(x, 6);

        var houses = new[]
        {
            new HouseRecord(new AddressId(1, 1, 1, 0), House1Lot, LotSize, House1Mailbox),
            new HouseRecord(new AddressId(1, 1, 2, 0), House2Lot, LotSize, House2Mailbox),
        };
        var lots = new[]
        {
            new LotRecord(1, 1, 1, new TileRect(House1Lot.X, House1Lot.Y, LotSize.X, LotSize.Y), true),
            new LotRecord(2, 1, 1, new TileRect(House2Lot.X, House2Lot.Y, LotSize.X, LotSize.Y), true),
        };

        return new WorldTables(
            Width,
            Height,
            TileCm,
            heights,
            houses,
            buildable,
            heightmapAttempts: 1,
            new PostOfficeRecord(PostOfficeTile, PostOfficeSize, SpawnPadTile, IntakeTile, Facing.East),
            new[] { new StreetRecord(1, StreetName, 1, streetTiles) },
            lots,
            Array.Empty<ResourceNodeRecord>(),
            Array.Empty<FerryLaneRecord>(),
            Array.Empty<RouteNodeRecord>(),
            Array.Empty<RouteEdgeRecord>(),
            Array.Empty<SpawnEdgeRecord>(),
            validationAttempts: 1);
    }
}
