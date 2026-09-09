using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

[Flags]
public enum MapLayer : byte
{
    None = 0,
    Districts = 1,
    Streets = 2,
}

[Flags]
public enum MapFilter : byte
{
    None = 0,
    Mail = 1,
    Routes = 2,
    Resources = 4,
}

public enum MapPingKind : byte
{
    Default = 0,
    DeliverHere = 1,
    BuildHere = 2,
    Danger = 3,
    NeedMaterials = 4,
}

public readonly record struct MapChip(string Id, string Label, bool On);

public readonly record struct MapDistrictMark(byte District, string Hex, DistrictPattern Pattern);

public readonly record struct MapStreetMark(
    byte Id,
    string Name,
    byte District,
    string Hex,
    IReadOnlyList<TileCoord> Tiles);

public readonly record struct MapHouseMark(
    AddressId Address,
    string Label,
    TileCoord Lot,
    TileCoord Size,
    bool HasMail);

public readonly record struct MapResourceMark(ResourceKind Kind, TileCoord Tile, string Label);

public readonly record struct MapRouteMark(int From, int To, TileCoord FromTile, TileCoord ToTile);

public readonly record struct MapPing(int Id, TileCoord Tile, MapPingKind Kind, uint PlacedTick);

public readonly record struct MapPingMark(int Id, TileCoord Tile, MapPingKind Kind, string Key);

public readonly record struct MapRaster(int CellsX, int CellsY, int CellTiles, byte[] Marks)
{
    public byte this[int cx, int cy] => Marks[cy * CellsX + cx];
}

public static class MapPingText
{
    public static string Key(MapPingKind kind) => kind switch
    {
        MapPingKind.Default => "default",
        MapPingKind.DeliverHere => "deliver",
        MapPingKind.BuildHere => "build",
        MapPingKind.Danger => "danger",
        MapPingKind.NeedMaterials => "materials",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string Label(MapPingKind kind) => kind switch
    {
        MapPingKind.Default => "Here",
        MapPingKind.DeliverHere => "Deliver here",
        MapPingKind.BuildHere => "Build here",
        MapPingKind.Danger => "Danger",
        MapPingKind.NeedMaterials => "Need materials",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string ResourceLabel(ResourceKind kind) => kind switch
    {
        ResourceKind.Wood => "wood",
        ResourceKind.Fiber => "fiber",
        ResourceKind.Stone => "stone",
        ResourceKind.IronOre => "iron",
        ResourceKind.Sand => "sand",
        ResourceKind.Berries => "berries",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
