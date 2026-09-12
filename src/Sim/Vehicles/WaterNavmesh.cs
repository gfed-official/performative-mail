using System;
using System.Collections.Generic;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Vehicles;

public static class WaterNavmesh
{
    public const int CoarseStepTiles = 4;
    public const byte SurfaceWater = 4;

    public static RoutingGraph Build(WorldTables tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        return Build(tables.Heights, tables.Width, tables.Height, tables.Ferries);
    }

    public static RoutingGraph Build(
        short[] heights,
        int width,
        int height,
        IReadOnlyList<FerryLaneRecord> ferries)
    {
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (ferries is null) throw new ArgumentNullException(nameof(ferries));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (heights.Length != width * height)
            throw new ArgumentException("Height buffer must be width × height.", nameof(heights));

        var tiles = new List<TileCoord>();
        var seen = new HashSet<long>();

        void Add(TileCoord tile)
        {
            if (!WorldGrid.InBounds(tile.X, tile.Y, width, height)) return;
            long key = ((long)tile.Y << 32) ^ (uint)tile.X;
            if (!seen.Add(key)) return;
            tiles.Add(tile);
        }

        for (int y = 0; y < height; y += CoarseStepTiles)
        {
            for (int x = 0; x < width; x += CoarseStepTiles)
            {
                if (IsDeep(heights, x, y, width))
                    Add(new TileCoord(x, y));
            }
        }

        for (int i = 0; i < ferries.Count; i++)
        {
            Add(ferries[i].A);
            Add(ferries[i].B);
        }

        tiles.Sort((a, b) =>
        {
            int cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        });

        var nodes = new RouteNodeRecord[tiles.Count];
        var at = new Dictionary<TileCoord, int>(tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
        {
            nodes[i] = new RouteNodeRecord(i, tiles[i]);
            at[tiles[i]] = i;
        }

        var edges = new List<RouteEdgeRecord>();
        var keys = new HashSet<long>();

        void AddEdge(int from, int to, int length, byte surface)
        {
            if (from == to) return;
            if (from > to)
            {
                int tmp = from;
                from = to;
                to = tmp;
            }

            if (length < 1) length = 1;
            long key = ((long)from << 32) | (uint)to;
            if (!keys.Add(key)) return;
            edges.Add(new RouteEdgeRecord(from, to, length, surface));
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            TryLink(tile, new TileCoord(tile.X + CoarseStepTiles, tile.Y));
            TryLink(tile, new TileCoord(tile.X, tile.Y + CoarseStepTiles));
            TryLink(tile, new TileCoord(tile.X + 1, tile.Y));
            TryLink(tile, new TileCoord(tile.X, tile.Y + 1));
        }

        void TryLink(TileCoord from, TileCoord to)
        {
            if (!at.TryGetValue(from, out int ia) || !at.TryGetValue(to, out int ib))
                return;
            if (!WaterCorridor(heights, width, height, from, to))
                return;
            AddEdge(ia, ib, WorldGrid.Manhattan(from.X, from.Y, to.X, to.Y), SurfaceWater);
        }

        for (int i = 0; i < ferries.Count; i++)
        {
            var lane = ferries[i];
            if (!at.TryGetValue(lane.A, out int ia) || !at.TryGetValue(lane.B, out int ib))
                continue;
            int len = WorldGrid.Manhattan(lane.A.X, lane.A.Y, lane.B.X, lane.B.Y);
            AddEdge(ia, ib, len, ConnectivityStage.SurfaceFerry);
            TryAttachBeach(lane.A, ia);
            TryAttachBeach(lane.B, ib);
        }

        void TryAttachBeach(TileCoord beach, int beachId)
        {
            int best = -1;
            int bestD = int.MaxValue;
            for (int n = 0; n < tiles.Count; n++)
            {
                if (n == beachId) continue;
                if (!IsDeep(heights, tiles[n].X, tiles[n].Y, width)) continue;
                if (!WaterCorridor(heights, width, height, beach, tiles[n])) continue;
                int d = WorldGrid.Manhattan(beach.X, beach.Y, tiles[n].X, tiles[n].Y);
                if (d < bestD || (d == bestD && n < best))
                {
                    bestD = d;
                    best = n;
                }
            }

            if (best >= 0)
                AddEdge(beachId, best, bestD, SurfaceWater);
        }

        return new RoutingGraph(nodes, edges);
    }

    private static bool WaterCorridor(short[] heights, int width, int height, TileCoord from, TileCoord to)
    {
        if (from.X != to.X && from.Y != to.Y)
            return false;

        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        int x = from.X;
        int y = from.Y;
        while (x != to.X || y != to.Y)
        {
            x += dx;
            y += dy;
            if (!WorldGrid.InBounds(x, y, width, height))
                return false;
            if (HeightmapStage.IsLand(heights[WorldGrid.Idx(x, y, width)]))
                return false;
        }

        return true;
    }

    private static bool IsDeep(short[] heights, int x, int y, int width) =>
        heights[WorldGrid.Idx(x, y, width)] <= BiomeStage.DeepCm;
}
