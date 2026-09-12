using System;
using System.Collections.Generic;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Vehicles;

public readonly record struct RouteEnemy(int Xcm, int Ycm, int Hp)
{
    public static RouteEnemy AtMeters(double x, double y, int hp) =>
        new(QuantizeCm(x), QuantizeCm(y), hp);

    private static int QuantizeCm(double meters) =>
        (int)Math.Round(meters * 100.0, MidpointRounding.AwayFromZero);
}

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

    public static bool EnemyWithin(
        IReadOnlyList<TileCoord> tiles,
        int tileCm,
        IReadOnlyList<RouteEnemy> enemies,
        double metres)
    {
        if (tiles is null) throw new ArgumentNullException(nameof(tiles));
        if (enemies is null) throw new ArgumentNullException(nameof(enemies));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm), tileCm, null);
        if (metres < 0 || double.IsNaN(metres) || double.IsInfinity(metres))
            throw new ArgumentOutOfRangeException(nameof(metres), metres, null);
        if (tiles.Count == 0 || enemies.Count == 0)
            return false;

        double tileM = tileCm / 100.0;
        double limitSq = metres * metres;
        for (int e = 0; e < enemies.Count; e++)
        {
            double px = enemies[e].Xcm / 100.0;
            double py = enemies[e].Ycm / 100.0;
            if (tiles.Count == 1)
            {
                if (DistSq(px, py, CenterX(tiles[0], tileM), CenterY(tiles[0], tileM)) <= limitSq)
                    return true;
                continue;
            }

            for (int i = 0; i < tiles.Count - 1; i++)
            {
                if (SegDistSq(
                        px,
                        py,
                        CenterX(tiles[i], tileM),
                        CenterY(tiles[i], tileM),
                        CenterX(tiles[i + 1], tileM),
                        CenterY(tiles[i + 1], tileM)) <= limitSq)
                    return true;
            }
        }

        return false;
    }

    private static double CenterX(TileCoord tile, double tileM) => (tile.X + 0.5) * tileM;

    private static double CenterY(TileCoord tile, double tileM) => (tile.Y + 0.5) * tileM;

    private static double DistSq(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx;
        double dy = ay - by;
        return dx * dx + dy * dy;
    }

    private static double SegDistSq(
        double px,
        double py,
        double ax,
        double ay,
        double bx,
        double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double lenSq = dx * dx + dy * dy;
        double t = 0;
        if (lenSq > 0)
        {
            t = ((px - ax) * dx + (py - ay) * dy) / lenSq;
            if (t < 0) t = 0;
            if (t > 1) t = 1;
        }

        return DistSq(px, py, ax + dx * t, ay + dy * t);
    }
}
