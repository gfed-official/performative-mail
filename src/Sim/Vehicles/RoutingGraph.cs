using System;
using System.Collections.Generic;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Vehicles;

public readonly record struct RoutePath(
    IReadOnlyList<int> Nodes,
    IReadOnlyList<TileCoord> Tiles,
    int LengthTiles);

public sealed class RoutingGraph
{
    private readonly RouteNodeRecord[] _nodes;
    private readonly Dictionary<int, int> _index;
    private readonly Dictionary<TileCoord, int> _atTile;
    private readonly List<(int To, int Length)>[] _adj;

    public RoutingGraph(IReadOnlyList<RouteNodeRecord> nodes, IReadOnlyList<RouteEdgeRecord> edges)
    {
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));
        if (edges is null) throw new ArgumentNullException(nameof(edges));

        _nodes = new RouteNodeRecord[nodes.Count];
        _index = new Dictionary<int, int>(nodes.Count);
        _atTile = new Dictionary<TileCoord, int>(nodes.Count);
        _adj = new List<(int To, int Length)>[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (_index.ContainsKey(node.Id))
                throw new ArgumentException($"Duplicate route node id {node.Id}.", nameof(nodes));
            _nodes[i] = node;
            _index.Add(node.Id, i);
            _atTile[node.Tile] = i;
            _adj[i] = new List<(int To, int Length)>();
        }

        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            if (!_index.TryGetValue(edge.From, out int from) || !_index.TryGetValue(edge.To, out int to))
                throw new ArgumentException($"Edge {edge.From}-{edge.To} references an unknown node.", nameof(edges));
            int length = edge.LengthTiles < 1 ? 1 : edge.LengthTiles;
            _adj[from].Add((to, length));
            _adj[to].Add((from, length));
        }
    }

    public bool TryNearestNode(TileCoord tile, out int nodeId)
    {
        nodeId = 0;
        if (_nodes.Length == 0) return false;
        if (_atTile.TryGetValue(tile, out int exact))
        {
            nodeId = _nodes[exact].Id;
            return true;
        }

        int bestDist = int.MaxValue;
        int bestId = int.MaxValue;
        int best = -1;
        for (int i = 0; i < _nodes.Length; i++)
        {
            int dist = Manhattan(tile, _nodes[i].Tile);
            int id = _nodes[i].Id;
            if (dist < bestDist || (dist == bestDist && id < bestId))
            {
                bestDist = dist;
                bestId = id;
                best = i;
            }
        }

        if (best < 0) return false;
        nodeId = _nodes[best].Id;
        return true;
    }

    public bool TryPath(TileCoord from, TileCoord to, out RoutePath path)
    {
        path = default;
        if (!TryNearestNode(from, out int src) || !TryNearestNode(to, out int dst))
            return false;
        return TryPath(src, dst, out path);
    }

    public bool TryPath(int from, int to, out RoutePath path)
    {
        path = default;
        if (!_index.TryGetValue(from, out int src) || !_index.TryGetValue(to, out int dst))
            return false;
        if (from == to)
        {
            path = new RoutePath(new[] { from }, new[] { _nodes[src].Tile }, 0);
            return true;
        }

        int n = _nodes.Length;
        var dist = new int[n];
        var prev = new int[n];
        var seen = new bool[n];
        for (int i = 0; i < n; i++)
        {
            dist[i] = int.MaxValue;
            prev[i] = -1;
        }

        dist[src] = 0;
        for (int iter = 0; iter < n; iter++)
        {
            int u = -1;
            int best = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (seen[i] || dist[i] >= best) continue;
                best = dist[i];
                u = i;
            }

            if (u < 0 || u == dst) break;
            seen[u] = true;
            var adj = _adj[u];
            for (int e = 0; e < adj.Count; e++)
            {
                var step = adj[e];
                if (dist[u] == int.MaxValue) continue;
                int next = dist[u] + step.Length;
                if (next < dist[step.To] || (next == dist[step.To] && (prev[step.To] < 0 || u < prev[step.To])))
                {
                    dist[step.To] = next;
                    prev[step.To] = u;
                }
            }
        }

        if (dist[dst] == int.MaxValue) return false;

        var stack = new List<int>();
        for (int at = dst; at >= 0; at = prev[at])
            stack.Add(_nodes[at].Id);
        stack.Reverse();

        var tiles = new TileCoord[stack.Count];
        for (int i = 0; i < stack.Count; i++)
            tiles[i] = _nodes[_index[stack[i]]].Tile;
        path = new RoutePath(stack, tiles, dist[dst]);
        return true;
    }

    public bool TryRoundTrip(TileCoord depot, IReadOnlyList<TileCoord> stops, out RoutePath path)
    {
        if (stops is null) throw new ArgumentNullException(nameof(stops));

        var waypoints = new TileCoord[stops.Count + 2];
        waypoints[0] = depot;
        for (int i = 0; i < stops.Count; i++)
            waypoints[i + 1] = stops[i];
        waypoints[waypoints.Length - 1] = depot;
        return TryWalk(waypoints, out path);
    }

    private bool TryWalk(IReadOnlyList<TileCoord> waypoints, out RoutePath path)
    {
        path = default;
        var nodes = new List<int>();
        var tiles = new List<TileCoord>();
        int length = 0;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (!TryPath(waypoints[i], waypoints[i + 1], out var hop))
                return false;
            int start = nodes.Count == 0 ? 0 : 1;
            for (int n = start; n < hop.Nodes.Count; n++)
            {
                nodes.Add(hop.Nodes[n]);
                tiles.Add(hop.Tiles[n]);
            }

            length += hop.LengthTiles;
        }

        path = new RoutePath(nodes, tiles, length);
        return true;
    }

    private static int Manhattan(TileCoord a, TileCoord b)
    {
        int dx = a.X - b.X;
        if (dx < 0) dx = -dx;
        int dy = a.Y - b.Y;
        if (dy < 0) dy = -dy;
        return dx + dy;
    }
}
