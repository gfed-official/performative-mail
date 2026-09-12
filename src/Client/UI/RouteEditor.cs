using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public readonly record struct RouteEditorStop(
    int Index,
    RouteStop Stop,
    TileCoord Tile,
    string Label);

public sealed class RouteEditor
{
    public const int DistrictLabelHitTiles = 1;

    private readonly IRouteConsole _console;
    private readonly WorldTables _world;
    private readonly RoutingGraph _graph;
    private readonly RouteAnchors _anchors;
    private readonly Dictionary<byte, TileCoord> _districtLabels = new Dictionary<byte, TileCoord>();

    public RouteEditor(IRouteConsole console, WorldTables world)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _graph = new RoutingGraph(world.RouteNodes, world.RouteEdges);
        _anchors = AnchorsOf(world);
        CacheDistrictLabels();
    }

    public IRouteConsole Console => _console;

    public VehicleRoute Route => _console.Route;

    public IReadOnlyList<RouteStop> Stops => _console.Route.Stops;

    public bool TryAddHouse(AddressId address)
    {
        if (!TryHouse(address, out _))
            return false;
        Append(RouteStop.ForAddress(address));
        return true;
    }

    public bool TryAddDistrict(byte district)
    {
        if (ExpandDistrict(district).Count == 0)
            return false;
        Append(RouteStop.ForDistrict(district));
        return true;
    }

    public bool TryClickTile(TileCoord tile)
    {
        if (TryHouseAt(tile, out var address))
            return TryAddHouse(address);
        if (TryDistrictLabelAt(tile, out byte district))
            return TryAddDistrict(district);
        return false;
    }

    public void MoveStop(int from, int to)
    {
        var current = _console.Route.Stops;
        if ((uint)from >= (uint)current.Count)
            throw new ArgumentOutOfRangeException(nameof(from), from, null);
        if ((uint)to >= (uint)current.Count)
            throw new ArgumentOutOfRangeException(nameof(to), to, null);
        if (from == to)
            return;

        var next = new RouteStop[current.Count];
        int n = 0;
        for (int i = 0; i < current.Count; i++)
        {
            if (i == from)
                continue;
            if (n == to)
                next[n++] = current[from];
            next[n++] = current[i];
        }

        if (n == to)
            next[n] = current[from];
        _console.Route.ReplaceStops(next);
    }

    public IReadOnlyList<AddressId> ExpandDistrict(byte district)
    {
        var addresses = new List<AddressId>();
        var houses = _world.Houses;
        for (int i = 0; i < houses.Length; i++)
        {
            if (houses[i].Address.District == district)
                addresses.Add(houses[i].Address);
        }

        return addresses;
    }

    public bool TryEstimate(out int lengthTiles, out int seconds)
    {
        lengthTiles = 0;
        seconds = 0;
        if (!_console.Route.TryRoundTrip(_graph, _console.ParkingZone, _anchors, out var trip))
            return false;
        lengthTiles = trip.LengthTiles;
        double metres = trip.LengthTiles * (_world.TileCm / 100.0);
        seconds = (int)Math.Round(
            metres / VehicleContext.MailTruckOnRoadMetersPerSecond,
            MidpointRounding.AwayFromZero);
        return true;
    }

    public IReadOnlyList<RouteEditorStop> Marks()
    {
        var stops = _console.Route.Stops;
        var marks = new RouteEditorStop[stops.Count];
        for (int i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            _anchors.TryResolve(stop, out var tile);
            marks[i] = new RouteEditorStop(i, stop, tile, LabelOf(stop));
        }

        return marks;
    }

    public bool TryHouseAt(TileCoord tile, out AddressId address)
    {
        var houses = _world.Houses;
        for (int i = 0; i < houses.Length; i++)
        {
            if (!houses[i].Lot.Contains(tile))
                continue;
            address = houses[i].Address;
            return true;
        }

        address = default;
        return false;
    }

    public bool TryDistrictLabelAt(TileCoord tile, out byte district)
    {
        foreach (var pair in _districtLabels)
        {
            if (Chebyshev(tile, pair.Value) <= DistrictLabelHitTiles)
            {
                district = pair.Key;
                return true;
            }
        }

        district = 0;
        return false;
    }

    public TileCoord DistrictLabel(byte district) =>
        _districtLabels.TryGetValue(district, out var tile) ? tile : default;

    public static string LabelOf(in RouteStop stop)
    {
        switch (stop.Kind)
        {
            case RouteStopKind.Address:
                return OverlayCell.MiniAddress(stop.Address);
            case RouteStopKind.District:
                return "D" + stop.District.ToString();
            case RouteStopKind.Construct:
                return "C" + stop.Construct.Counter.ToString();
            default:
            {
                RouteStopKind unseen = stop.Kind;
                throw new ArgumentOutOfRangeException(nameof(stop), unseen, null);
            }
        }
    }

    private void Append(RouteStop stop)
    {
        var current = _console.Route.Stops;
        var next = new RouteStop[current.Count + 1];
        for (int i = 0; i < current.Count; i++)
            next[i] = current[i];
        next[current.Count] = stop;
        _console.Route.ReplaceStops(next);
    }

    private bool TryHouse(AddressId address, out HouseRecord house)
    {
        var houses = _world.Houses;
        for (int i = 0; i < houses.Length; i++)
        {
            if (!houses[i].Address.Equals(address))
                continue;
            house = houses[i];
            return true;
        }

        house = default;
        return false;
    }

    private void CacheDistrictLabels()
    {
        var seen = new HashSet<byte>();
        var houses = _world.Houses;
        for (int i = 0; i < houses.Length; i++)
            seen.Add(houses[i].Address.District);
        var streets = _world.Streets;
        for (int i = 0; i < streets.Length; i++)
            seen.Add(streets[i].District);
        foreach (byte district in seen)
        {
            if (DistrictPalette.HasSwatch(district))
                _districtLabels[district] = MapFrame.DistrictLabelTile(_world, district);
        }
    }

    private static RouteAnchors AnchorsOf(WorldTables world)
    {
        var anchors = new RouteAnchors();
        var houses = world.Houses;
        for (int i = 0; i < houses.Length; i++)
            anchors.AddAddress(houses[i].Address, houses[i].Mailbox.Tile(world.TileCm));
        return anchors;
    }

    private static int Chebyshev(TileCoord a, TileCoord b)
    {
        int dx = a.X - b.X;
        if (dx < 0) dx = -dx;
        int dy = a.Y - b.Y;
        if (dy < 0) dy = -dy;
        return dx > dy ? dx : dy;
    }
}
