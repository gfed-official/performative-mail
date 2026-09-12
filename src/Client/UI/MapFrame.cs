using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public readonly record struct MapFrame(
    int Width,
    int Height,
    MapLayer Layers,
    MapFilter Filters,
    MapRaster Raster,
    IReadOnlyList<MapDistrictMark> Districts,
    IReadOnlyList<MapStreetMark> Streets,
    IReadOnlyList<MapHouseMark> Houses,
    IReadOnlyList<MapResourceMark> Resources,
    IReadOnlyList<MapRouteMark> Routes,
    IReadOnlyList<MapPingMark> Pings,
    IReadOnlyList<MapChip> Chips)
{
    public const MapLayer DefaultLayers = MapLayer.Districts | MapLayer.Streets;

    public static MapFrame Empty(MapLayer layers, MapFilter filters) =>
        new(
            0,
            0,
            layers,
            filters,
            new MapRaster(0, 0, 1, Array.Empty<byte>()),
            Array.Empty<MapDistrictMark>(),
            Array.Empty<MapStreetMark>(),
            Array.Empty<MapHouseMark>(),
            Array.Empty<MapResourceMark>(),
            Array.Empty<MapRouteMark>(),
            Array.Empty<MapPingMark>(),
            ChipsOf(layers, filters));

    public static MapFrame From(
        WorldTables? world,
        OverlayReplica? overlay,
        MapLayer layers,
        MapFilter filters,
        IReadOnlyList<MapPing> pings)
    {
        if (world is null)
            return Empty(layers, filters);

        var mail = MailAddresses(overlay);
        var districts = DistrictsOf(world);
        var streets = StreetsOf(world);
        var houses = HousesOf(world, mail);
        var resources = ResourcesOf(world);
        var routes = RoutesOf(world);
        var marks = PingsOf(pings);
        return new MapFrame(
            world.Width,
            world.Height,
            layers,
            filters,
            RasterOf(world),
            districts,
            streets,
            houses,
            resources,
            routes,
            marks,
            ChipsOf(layers, filters));
    }

    public static bool SameDisplay(in MapFrame a, in MapFrame b) =>
        a.Width == b.Width
        && a.Height == b.Height
        && a.Layers == b.Layers
        && a.Filters == b.Filters
        && a.Districts.Count == b.Districts.Count
        && a.Streets.Count == b.Streets.Count
        && a.Houses.Count == b.Houses.Count
        && a.Resources.Count == b.Resources.Count
        && a.Routes.Count == b.Routes.Count
        && a.Pings.Count == b.Pings.Count
        && SameHouses(a.Houses, b.Houses)
        && SamePings(a.Pings, b.Pings);

    public static HashSet<AddressId> MailAddresses(OverlayReplica? overlay)
    {
        var set = new HashSet<AddressId>();
        if (overlay is not { } live)
            return set;

        Collect(live.Hotbar, set);
        Collect(live.Inventory, set);
        if (live.Backpack is { } pack)
            Collect(pack, set);
        if (live.External is { } ext)
            Collect(ext, set);
        return set;
    }

    private static void Collect(GridContainer grid, HashSet<AddressId> set)
    {
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is MailStack mail)
                set.Add(mail.Address);
        }
    }

    private static MapChip[] ChipsOf(MapLayer layers, MapFilter filters) =>
        new[]
        {
            new MapChip("districts", "Districts", layers.HasFlag(MapLayer.Districts)),
            new MapChip("streets", "Streets", layers.HasFlag(MapLayer.Streets)),
            new MapChip("mail", "Mail", filters.HasFlag(MapFilter.Mail)),
            new MapChip("routes", "Routes", filters.HasFlag(MapFilter.Routes)),
            new MapChip("resources", "Resources", filters.HasFlag(MapFilter.Resources)),
        };

    private static MapDistrictMark[] DistrictsOf(WorldTables world)
    {
        var seen = new HashSet<byte>();
        var marks = new List<MapDistrictMark>();
        var streets = world.Streets;
        for (int i = 0; i < streets.Length; i++)
            AddDistrict(world, seen, marks, streets[i].District);
        var lots = world.Lots;
        for (int i = 0; i < lots.Length; i++)
            AddDistrict(world, seen, marks, lots[i].District);
        var houses = world.Houses;
        for (int i = 0; i < houses.Length; i++)
            AddDistrict(world, seen, marks, houses[i].Address.District);
        marks.Sort((a, b) => a.District.CompareTo(b.District));
        return marks.ToArray();
    }

    public static TileCoord DistrictLabelTile(WorldTables world, byte district)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        int sx = 0;
        int sy = 0;
        int n = 0;
        var streets = world.Streets;
        for (int i = 0; i < streets.Length; i++)
        {
            if (streets[i].District != district)
                continue;
            var tiles = streets[i].Tiles;
            if (tiles is null)
                continue;
            for (int t = 0; t < tiles.Length; t++)
            {
                sx += tiles[t].X;
                sy += tiles[t].Y;
                n++;
            }
        }

        if (n == 0)
        {
            var houses = world.Houses;
            for (int i = 0; i < houses.Length; i++)
            {
                if (houses[i].Address.District != district)
                    continue;
                sx += houses[i].LotTile.X;
                sy += houses[i].LotTile.Y;
                n++;
            }
        }

        return n == 0 ? default : new TileCoord(sx / n, sy / n);
    }

    private static void AddDistrict(
        WorldTables world,
        HashSet<byte> seen,
        List<MapDistrictMark> marks,
        byte district)
    {
        if (!DistrictPalette.HasSwatch(district) || !seen.Add(district))
            return;
        byte index = DistrictPalette.IndexOf(district);
        marks.Add(new MapDistrictMark(
            district,
            DistrictPalette.Hex(index),
            DistrictPalette.Pattern(index),
            DistrictLabelTile(world, district),
            "D" + district.ToString()));
    }

    private static MapStreetMark[] StreetsOf(WorldTables world)
    {
        var streets = world.Streets;
        var marks = new MapStreetMark[streets.Length];
        for (int i = 0; i < streets.Length; i++)
        {
            var street = streets[i];
            var tiles = street.Tiles is null ? Array.Empty<TileCoord>() : street.Tiles;
            marks[i] = new MapStreetMark(
                street.Id,
                street.Name ?? "",
                street.District,
                DistrictPalette.HexOf(street.District),
                tiles);
        }

        return marks;
    }

    private static MapHouseMark[] HousesOf(WorldTables world, HashSet<AddressId> mail)
    {
        var houses = world.Houses;
        var marks = new MapHouseMark[houses.Length];
        for (int i = 0; i < houses.Length; i++)
        {
            var house = houses[i];
            marks[i] = new MapHouseMark(
                house.Address,
                OverlayCell.MiniAddress(house.Address),
                house.LotTile,
                house.LotSizeTiles,
                mail.Contains(house.Address));
        }

        return marks;
    }

    private static MapResourceMark[] ResourcesOf(WorldTables world)
    {
        var nodes = world.ResourceNodes;
        var marks = new MapResourceMark[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            marks[i] = new MapResourceMark(node.Kind, node.Tile, MapPingText.ResourceLabel(node.Kind));
        }

        return marks;
    }

    private static MapRouteMark[] RoutesOf(WorldTables world)
    {
        var nodes = world.RouteNodes;
        var edges = world.RouteEdges;
        var at = new Dictionary<int, TileCoord>(nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
            at[nodes[i].Id] = nodes[i].Tile;

        var marks = new List<MapRouteMark>(edges.Length);
        for (int i = 0; i < edges.Length; i++)
        {
            var edge = edges[i];
            if (!at.TryGetValue(edge.From, out var from) || !at.TryGetValue(edge.To, out var to))
                continue;
            marks.Add(new MapRouteMark(edge.From, edge.To, from, to));
        }

        return marks.ToArray();
    }

    private static MapPingMark[] PingsOf(IReadOnlyList<MapPing> pings)
    {
        if (pings is null || pings.Count == 0)
            return Array.Empty<MapPingMark>();

        var marks = new MapPingMark[pings.Count];
        for (int i = 0; i < pings.Count; i++)
        {
            var ping = pings[i];
            marks[i] = new MapPingMark(ping.Id, ping.Tile, ping.Kind, MapPingText.Key(ping.Kind));
        }

        return marks;
    }

    private static MapRaster RasterOf(WorldTables world)
    {
        int cellTiles = CellTilesOf(world.Width, world.Height);
        int cellsX = Math.Max(1, world.Width / cellTiles);
        int cellsY = Math.Max(1, world.Height / cellTiles);
        var marks = new byte[cellsX * cellsY];
        var streets = world.Streets;
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null)
                continue;
            for (int t = 0; t < tiles.Length; t++)
                Mark(marks, cellsX, cellsY, cellTiles, tiles[t].X, tiles[t].Y, streets[s].District);
        }

        var houses = world.Houses;
        for (int h = 0; h < houses.Length; h++)
        {
            var lot = houses[h].Lot;
            for (int y = 0; y < lot.Height; y++)
            {
                for (int x = 0; x < lot.Width; x++)
                    Mark(marks, cellsX, cellsY, cellTiles, lot.X + x, lot.Y + y, houses[h].Address.District);
            }
        }

        return new MapRaster(cellsX, cellsY, cellTiles, marks);
    }

    internal static int CellTilesOf(int width, int height) =>
        Math.Max(width, height) > 48 ? SeedView.CellTiles : 1;

    private static void Mark(byte[] marks, int cellsX, int cellsY, int cellTiles, int tileX, int tileY, byte district)
    {
        if (district == 0)
            return;
        int cx = tileX / cellTiles;
        int cy = tileY / cellTiles;
        if ((uint)cx >= (uint)cellsX || (uint)cy >= (uint)cellsY)
            return;
        marks[cy * cellsX + cx] = district;
    }

    private static bool SameHouses(IReadOnlyList<MapHouseMark> a, IReadOnlyList<MapHouseMark> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Address != b[i].Address || a[i].HasMail != b[i].HasMail)
                return false;
        }

        return true;
    }

    private static bool SamePings(IReadOnlyList<MapPingMark> a, IReadOnlyList<MapPingMark> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Id != b[i].Id || a[i].Tile != b[i].Tile || a[i].Kind != b[i].Kind)
                return false;
        }

        return true;
    }
}
