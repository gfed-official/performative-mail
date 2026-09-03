using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public sealed class WorldTables
{
    public WorldTables(
        int width,
        int height,
        int tileCm,
        short[] heights,
        HouseRecord[] houses,
        bool[] buildable,
        int heightmapAttempts,
        PostOfficeRecord postOffice,
        StreetRecord[] streets,
        LotRecord[] lots,
        ResourceNodeRecord[] resourceNodes,
        FerryLaneRecord[] ferries,
        RouteNodeRecord[] routeNodes,
        RouteEdgeRecord[] routeEdges,
        SpawnEdgeRecord[] spawnEdges,
        int validationAttempts)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));
        if (heightmapAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(heightmapAttempts));
        if (validationAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(validationAttempts));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (houses is null) throw new ArgumentNullException(nameof(houses));
        if (buildable is null) throw new ArgumentNullException(nameof(buildable));
        if (streets is null) throw new ArgumentNullException(nameof(streets));
        if (lots is null) throw new ArgumentNullException(nameof(lots));
        if (resourceNodes is null) throw new ArgumentNullException(nameof(resourceNodes));
        if (ferries is null) throw new ArgumentNullException(nameof(ferries));
        if (routeNodes is null) throw new ArgumentNullException(nameof(routeNodes));
        if (routeEdges is null) throw new ArgumentNullException(nameof(routeEdges));
        if (spawnEdges is null) throw new ArgumentNullException(nameof(spawnEdges));
        if (heights.Length != width * height)
            throw new ArgumentException("Height buffer must be width × height.", nameof(heights));
        if (buildable.Length != heights.Length)
            throw new ArgumentException("Buildable buffer must match height buffer.", nameof(buildable));

        Width = width;
        Height = height;
        TileCm = tileCm;
        Heights = (short[])heights.Clone();
        Houses = (HouseRecord[])houses.Clone();
        Addresses = AddressesOf(Houses);
        Buildable = (bool[])buildable.Clone();
        HeightmapAttempts = heightmapAttempts;
        PostOffice = postOffice;
        Streets = CloneStreets(streets);
        Lots = (LotRecord[])lots.Clone();
        ResourceNodes = (ResourceNodeRecord[])resourceNodes.Clone();
        Ferries = (FerryLaneRecord[])ferries.Clone();
        RouteNodes = (RouteNodeRecord[])routeNodes.Clone();
        RouteEdges = (RouteEdgeRecord[])routeEdges.Clone();
        SpawnEdges = CloneSpawns(spawnEdges);
        ValidationAttempts = validationAttempts;
        Valid = ValidationStage.Evaluate(this);
    }

    public int Width { get; }

    public int Height { get; }

    public int TileCm { get; }

    public short[] Heights { get; }

    public AddressId[] Addresses { get; }

    public HouseRecord[] Houses { get; }

    public bool[] Buildable { get; }

    public bool Valid { get; }

    public int HeightmapAttempts { get; }

    public PostOfficeRecord PostOffice { get; }

    public StreetRecord[] Streets { get; }

    public LotRecord[] Lots { get; }

    public ResourceNodeRecord[] ResourceNodes { get; }

    public FerryLaneRecord[] Ferries { get; }

    public RouteNodeRecord[] RouteNodes { get; }

    public RouteEdgeRecord[] RouteEdges { get; }

    public SpawnEdgeRecord[] SpawnEdges { get; }

    public int ValidationAttempts { get; }

    private static AddressId[] AddressesOf(HouseRecord[] houses)
    {
        var addresses = new AddressId[houses.Length];
        for (int i = 0; i < houses.Length; i++)
            addresses[i] = houses[i].Address;
        Array.Sort(addresses, (a, b) => a.Packed.CompareTo(b.Packed));
        return addresses;
    }

    private static StreetRecord[] CloneStreets(StreetRecord[] streets)
    {
        var copy = new StreetRecord[streets.Length];
        for (int i = 0; i < streets.Length; i++)
        {
            var street = streets[i];
            var tiles = street.Tiles is null ? Array.Empty<TileCoord>() : (TileCoord[])street.Tiles.Clone();
            copy[i] = new StreetRecord(street.Id, street.Name, street.District, tiles);
        }

        return copy;
    }

    private static SpawnEdgeRecord[] CloneSpawns(SpawnEdgeRecord[] edges)
    {
        var copy = new SpawnEdgeRecord[edges.Length];
        for (int i = 0; i < edges.Length; i++)
        {
            var edge = edges[i];
            var path = edge.PathToPo is null ? Array.Empty<TileCoord>() : (TileCoord[])edge.PathToPo.Clone();
            copy[i] = new SpawnEdgeRecord(edge.District, edge.Tile, path);
        }

        return copy;
    }
}
