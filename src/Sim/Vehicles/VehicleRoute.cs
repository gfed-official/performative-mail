using System;
using System.Collections.Generic;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Vehicles;

public readonly record struct RouteRoundTrip(
    IReadOnlyList<RouteStop> Stops,
    IReadOnlyList<TileCoord> Waypoints,
    IReadOnlyList<int> HopNodes,
    IReadOnlyList<int> Nodes,
    int LengthTiles);

public sealed class VehicleRoute
{
    private readonly List<RouteStop> _stops = new List<RouteStop>();

    public IReadOnlyList<RouteStop> Stops => _stops;

    public void ReplaceStops(IReadOnlyList<RouteStop> stops)
    {
        if (stops is null) throw new ArgumentNullException(nameof(stops));
        _stops.Clear();
        for (int i = 0; i < stops.Count; i++)
            _stops.Add(stops[i]);
    }

    public bool TryRoundTrip(
        RoutingGraph graph,
        TileCoord depot,
        RouteAnchors anchors,
        out RouteRoundTrip trip)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (anchors is null) throw new ArgumentNullException(nameof(anchors));

        trip = default;
        var waypoints = new TileCoord[_stops.Count + 2];
        waypoints[0] = depot;
        for (int i = 0; i < _stops.Count; i++)
        {
            if (!anchors.TryResolve(_stops[i], out var tile))
                return false;
            waypoints[i + 1] = tile;
        }

        waypoints[waypoints.Length - 1] = depot;
        if (!graph.TryRoundTrip(depot, SliceStops(waypoints), out var path))
            return false;

        var hops = new int[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (!graph.TryNearestNode(waypoints[i], out hops[i]))
                return false;
        }

        var stops = new RouteStop[_stops.Count];
        for (int i = 0; i < _stops.Count; i++)
            stops[i] = _stops[i];
        trip = new RouteRoundTrip(stops, waypoints, hops, path.Nodes, path.LengthTiles);
        return true;
    }

    private static TileCoord[] SliceStops(TileCoord[] waypoints)
    {
        var stops = new TileCoord[waypoints.Length - 2];
        for (int i = 0; i < stops.Length; i++)
            stops[i] = waypoints[i + 1];
        return stops;
    }
}
