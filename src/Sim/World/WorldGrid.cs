using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.World;

internal static class WorldGrid
{
    public static readonly int[] Dx = { 1, -1, 0, 0 };
    public static readonly int[] Dy = { 0, 0, 1, -1 };

    public static bool InBounds(int x, int y, int width, int height) =>
        (uint)x < (uint)width && (uint)y < (uint)height;

    public static int Idx(int x, int y, int width) => y * width + x;

    public static int Chebyshev(int x0, int y0, int x1, int y1)
    {
        int dx = x0 - x1;
        if (dx < 0) dx = -dx;
        int dy = y0 - y1;
        if (dy < 0) dy = -dy;
        return dx > dy ? dx : dy;
    }

    public static int DistSq(int x0, int y0, int x1, int y1)
    {
        int dx = x0 - x1;
        int dy = y0 - y1;
        return dx * dx + dy * dy;
    }

    public static int Manhattan(int x0, int y0, int x1, int y1)
    {
        int dx = x0 - x1;
        if (dx < 0) dx = -dx;
        int dy = y0 - y1;
        if (dy < 0) dy = -dy;
        return dx + dy;
    }

    public static int WalkStart(PostOfficeRecord po, bool[] walk, int width, int height)
    {
        int spawn = Idx(po.SpawnPadTile.X, po.SpawnPadTile.Y, width);
        if (InBounds(po.SpawnPadTile.X, po.SpawnPadTile.Y, width, height) && walk[spawn])
            return spawn;

        foreach (var tile in po.Footprint.Tiles())
        {
            if (!InBounds(tile.X, tile.Y, width, height)) continue;
            int i = Idx(tile.X, tile.Y, width);
            if (walk[i]) return i;
        }

        return spawn;
    }

    public static void FillOccupied(
        bool[] occ,
        int width,
        int height,
        PostOfficeRecord po,
        StreetRecord[] streets,
        LotRecord[] lots)
    {
        Array.Clear(occ, 0, occ.Length);
        foreach (var tile in po.Footprint.Tiles())
            Mark(occ, tile, width, height);
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null) continue;
            for (int i = 0; i < tiles.Length; i++)
                Mark(occ, tiles[i], width, height);
        }

        for (int l = 0; l < lots.Length; l++)
        {
            foreach (var tile in lots[l].Footprint.Tiles())
                Mark(occ, tile, width, height);
        }
    }

    public static void FillLotMask(bool[] lotsMask, int width, int height, LotRecord[] lots)
    {
        Array.Clear(lotsMask, 0, lotsMask.Length);
        for (int l = 0; l < lots.Length; l++)
        {
            foreach (var tile in lots[l].Footprint.Tiles())
                Mark(lotsMask, tile, width, height);
        }
    }

    public static void FillWalkable(
        bool[] walk,
        short[] heights,
        bool[] buildable,
        int width,
        int height,
        PostOfficeRecord po,
        StreetRecord[] streets)
    {
        int count = width * height;
        for (int i = 0; i < count; i++)
            walk[i] = HeightmapStage.IsLand(heights[i]) && buildable[i];

        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null) continue;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (!InBounds(tile.X, tile.Y, width, height)) continue;
                int idx = Idx(tile.X, tile.Y, width);
                if (HeightmapStage.IsLand(heights[idx]))
                    walk[idx] = true;
            }
        }

        foreach (var tile in po.Footprint.Tiles())
        {
            if (!InBounds(tile.X, tile.Y, width, height)) continue;
            int idx = Idx(tile.X, tile.Y, width);
            if (HeightmapStage.IsLand(heights[idx]))
                walk[idx] = true;
        }
    }

    public static void FillStreetMask(bool[] streetMask, int width, int height, StreetRecord[] streets)
    {
        Array.Clear(streetMask, 0, streetMask.Length);
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null) continue;
            for (int i = 0; i < tiles.Length; i++)
                Mark(streetMask, tiles[i], width, height);
        }
    }

    public static bool IsWalkableBeach(bool[] walk, short[] heights, int x, int y, int width, int height)
    {
        if (!InBounds(x, y, width, height)) return false;
        int i = Idx(x, y, width);
        if (!walk[i]) return false;
        for (int d = 0; d < 4; d++)
        {
            int nx = x + Dx[d];
            int ny = y + Dy[d];
            if (!InBounds(nx, ny, width, height)) continue;
            if (!HeightmapStage.IsLand(heights[Idx(nx, ny, width)]))
                return true;
        }

        return false;
    }

    public static void FerryPairs(
        FerryLaneRecord[] ferries,
        int width,
        int height,
        out int[] from,
        out int[] to)
    {
        var a = new List<int>(ferries.Length * 2);
        var b = new List<int>(ferries.Length * 2);
        for (int i = 0; i < ferries.Length; i++)
        {
            var f = ferries[i];
            if (!InBounds(f.A.X, f.A.Y, width, height) || !InBounds(f.B.X, f.B.Y, width, height))
                continue;
            int ia = Idx(f.A.X, f.A.Y, width);
            int ib = Idx(f.B.X, f.B.Y, width);
            a.Add(ia);
            b.Add(ib);
            a.Add(ib);
            b.Add(ia);
        }

        from = a.ToArray();
        to = b.ToArray();
    }

    public static void BfsWalk(
        bool[] walk,
        short[] heights,
        int width,
        int height,
        int start,
        bool[] seen,
        int[] parent,
        int[] ferryFrom,
        int[] ferryTo)
    {
        int count = width * height;
        Array.Clear(seen, 0, count);
        for (int i = 0; i < count; i++)
            parent[i] = -1;
        if ((uint)start >= (uint)count || !walk[start])
            return;

        var q = new int[count];
        int head = 0;
        int tail = 0;
        seen[start] = true;
        q[tail++] = start;

        while (head < tail)
        {
            int cur = q[head++];
            int x = cur % width;
            int y = cur / width;
            for (int d = 0; d < 4; d++)
            {
                int cx = x;
                int cy = y;
                int water = 0;
                for (int step = 0; step < 4; step++)
                {
                    cx += Dx[d];
                    cy += Dy[d];
                    if (!InBounds(cx, cy, width, height)) break;
                    int ni = Idx(cx, cy, width);
                    if (walk[ni])
                    {
                        if (water <= 3 && !seen[ni])
                        {
                            seen[ni] = true;
                            parent[ni] = cur;
                            q[tail++] = ni;
                        }

                        break;
                    }

                    if (HeightmapStage.IsLand(heights[ni])) break;
                    water++;
                    if (water > 3) break;
                }
            }

            for (int f = 0; f < ferryFrom.Length; f++)
            {
                if (ferryFrom[f] != cur) continue;
                int ni = ferryTo[f];
                if ((uint)ni >= (uint)count || seen[ni] || !walk[ni]) continue;
                seen[ni] = true;
                parent[ni] = cur;
                q[tail++] = ni;
            }
        }
    }

    public static TileCoord[]? PathFromToStart(int from, int start, int[] parent, int width)
    {
        if (from == start) return Array.Empty<TileCoord>();
        if ((uint)from >= (uint)parent.Length || parent[from] < 0) return null;

        int n = 0;
        int cur = from;
        while (cur != start)
        {
            cur = parent[cur];
            if (cur < 0) return null;
            n++;
            if (n > parent.Length) return null;
        }

        var path = new TileCoord[n];
        cur = from;
        for (int i = 0; i < n; i++)
        {
            cur = parent[cur];
            path[i] = new TileCoord(cur % width, cur / width);
        }

        return path;
    }

    private static void Mark(bool[] mask, TileCoord tile, int width, int height)
    {
        if (!InBounds(tile.X, tile.Y, width, height)) return;
        mask[Idx(tile.X, tile.Y, width)] = true;
    }
}
