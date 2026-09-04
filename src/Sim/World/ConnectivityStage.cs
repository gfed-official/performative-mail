using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

internal readonly struct ConnectivityResult
{
    public ConnectivityResult(
        FerryLaneRecord[] ferries,
        RouteNodeRecord[] routeNodes,
        RouteEdgeRecord[] routeEdges)
    {
        Ferries = ferries;
        RouteNodes = routeNodes;
        RouteEdges = routeEdges;
    }

    public FerryLaneRecord[] Ferries { get; }

    public RouteNodeRecord[] RouteNodes { get; }

    public RouteEdgeRecord[] RouteEdges { get; }
}

internal static class ConnectivityStage
{
    public const byte SurfaceStreet = 0;
    public const byte SurfaceDirt = 1;
    public const byte SurfaceBridge = 2;
    public const byte SurfaceFerry = 3;

    public static ConnectivityResult Generate(
        RngStream roads,
        short[] heights,
        bool[] buildable,
        PostOfficeRecord po,
        StreetRecord[] streets,
        HouseRecord[] houses,
        int width,
        int height,
        int tileCm)
    {
        if (roads is null) throw new ArgumentNullException(nameof(roads));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (buildable is null) throw new ArgumentNullException(nameof(buildable));
        if (streets is null) throw new ArgumentNullException(nameof(streets));
        if (houses is null) throw new ArgumentNullException(nameof(houses));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));

        int count = width * height;
        var walk = new bool[count];
        WorldGrid.FillWalkable(walk, heights, buildable, width, height, po, streets);

        var parentUf = new int[count];
        for (int i = 0; i < count; i++)
            parentUf[i] = i;

        int Find(int x)
        {
            int r = x;
            while (parentUf[r] != r)
                r = parentUf[r];
            while (parentUf[x] != r)
            {
                int n = parentUf[x];
                parentUf[x] = r;
                x = n;
            }

            return r;
        }

        void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a == b) return;
            if (a < b) parentUf[b] = a;
            else parentUf[a] = b;
        }

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                if (!walk[i]) continue;
                if (x + 1 < width && walk[i + 1]) Union(i, i + 1);
                if (y + 1 < height && walk[i + width]) Union(i, i + width);
            }
        }

        var bridges = new List<(int A, int B, int Gap)>();
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                if (!walk[i]) continue;
                TryBridge(x, y, i, 1, 0);
                TryBridge(x, y, i, 0, 1);
            }
        }

        void TryBridge(int x, int y, int origin, int dx, int dy)
        {
            int water = 0;
            int cx = x;
            int cy = y;
            for (int step = 0; step < 4; step++)
            {
                cx += dx;
                cy += dy;
                if (!WorldGrid.InBounds(cx, cy, width, height)) return;
                int ni = WorldGrid.Idx(cx, cy, width);
                if (walk[ni])
                {
                    if (water >= 1 && water <= 3 && Find(origin) != Find(ni))
                    {
                        bridges.Add((origin, ni, water));
                        Union(origin, ni);
                    }

                    return;
                }

                if (HeightmapStage.IsLand(heights[ni])) return;
                water++;
                if (water > 3) return;
            }
        }

        var seen = new bool[count];
        var parent = new int[count];
        int walkStart = WorldGrid.WalkStart(po, walk, width, height);
        int spawnPad = WorldGrid.Idx(po.SpawnPadTile.X, po.SpawnPadTile.Y, width);
        WorldGrid.BfsWalk(
            walk,
            heights,
            width,
            height,
            walkStart,
            seen,
            parent,
            Array.Empty<int>(),
            Array.Empty<int>());

        var poBeaches = new List<int>();
        for (int i = 0; i < count; i++)
        {
            if (!seen[i]) continue;
            int x = i % width;
            int y = i / width;
            if (WorldGrid.IsWalkableBeach(walk, heights, x, y, width, height))
                poBeaches.Add(i);
        }

        var ferries = new List<FerryLaneRecord>();
        var ferryKeys = new HashSet<long>();
        var handled = new bool[houses.Length];
        for (int h = 0; h < houses.Length; h++)
        {
            if (handled[h]) continue;
            var mailbox = houses[h].Mailbox.Tile(tileCm);
            if (!WorldGrid.InBounds(mailbox.X, mailbox.Y, width, height)) continue;
            int mi = WorldGrid.Idx(mailbox.X, mailbox.Y, width);
            if (seen[mi])
            {
                handled[h] = true;
                continue;
            }

            var houseSeen = new bool[count];
            var houseParent = new int[count];
            WorldGrid.BfsWalk(
                walk,
                heights,
                width,
                height,
                mi,
                houseSeen,
                houseParent,
                Array.Empty<int>(),
                Array.Empty<int>());

            var houseBeaches = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (!houseSeen[i]) continue;
                int x = i % width;
                int y = i / width;
                if (WorldGrid.IsWalkableBeach(walk, heights, x, y, width, height))
                    houseBeaches.Add(i);
            }

            if (poBeaches.Count > 0 && houseBeaches.Count > 0)
            {
                int bestA = poBeaches[0];
                int bestB = houseBeaches[0];
                int bestD = int.MaxValue;
                for (int a = 0; a < poBeaches.Count; a++)
                {
                    int ia = poBeaches[a];
                    int ax = ia % width;
                    int ay = ia / width;
                    for (int b = 0; b < houseBeaches.Count; b++)
                    {
                        int ib = houseBeaches[b];
                        int d = WorldGrid.DistSq(ax, ay, ib % width, ib / width);
                        if (d < bestD || (d == bestD && (ia < bestA || (ia == bestA && ib < bestB))))
                        {
                            bestD = d;
                            bestA = ia;
                            bestB = ib;
                        }
                    }
                }

                int lo = bestA < bestB ? bestA : bestB;
                int hi = bestA < bestB ? bestB : bestA;
                long key = ((long)lo << 32) | (uint)hi;
                if (ferryKeys.Add(key))
                {
                    ferries.Add(new FerryLaneRecord(
                        new TileCoord(bestA % width, bestA / width),
                        new TileCoord(bestB % width, bestB / width)));
                }
            }

            for (int o = h; o < houses.Length; o++)
            {
                if (handled[o]) continue;
                var om = houses[o].Mailbox.Tile(tileCm);
                if (!WorldGrid.InBounds(om.X, om.Y, width, height)) continue;
                int oi = WorldGrid.Idx(om.X, om.Y, width);
                if (houseSeen[oi])
                    handled[o] = true;
            }
        }

        var ferryArr = ferries.ToArray();
        if (streets.Length == 0)
            return new ConnectivityResult(ferryArr, Array.Empty<RouteNodeRecord>(), Array.Empty<RouteEdgeRecord>());

        var streetMask = new bool[count];
        WorldGrid.FillStreetMask(streetMask, width, height, streets);

        var nodeTiles = new List<TileCoord>();
        var nodeSeen = new HashSet<long>();

        void AddNode(int x, int y)
        {
            if (!WorldGrid.InBounds(x, y, width, height)) return;
            long key = ((long)y << 32) ^ (uint)x;
            if (!nodeSeen.Add(key)) return;
            nodeTiles.Add(new TileCoord(x, y));
        }

        var cover = new int[count];
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null) continue;
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                if (!WorldGrid.InBounds(t.X, t.Y, width, height)) continue;
                cover[WorldGrid.Idx(t.X, t.Y, width)]++;
            }
        }

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (cover[row + x] >= 2)
                    AddNode(x, y);
            }
        }

        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null || tiles.Length == 0) continue;
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                if (t.X < minX) minX = t.X;
                if (t.X > maxX) maxX = t.X;
                if (t.Y < minY) minY = t.Y;
                if (t.Y > maxY) maxY = t.Y;
            }

            bool eastWest = (maxX - minX) >= (maxY - minY);
            int endA = int.MaxValue;
            int endB = int.MinValue;
            int pickA = 0;
            int pickB = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                int axis = eastWest ? t.X : t.Y;
                int other = eastWest ? t.Y : t.X;
                if (axis < endA || (axis == endA && other < pickA))
                {
                    endA = axis;
                    pickA = other;
                }

                if (axis > endB || (axis == endB && other < pickB))
                {
                    endB = axis;
                    pickB = other;
                }
            }

            if (eastWest)
            {
                AddNode(endA, pickA);
                AddNode(endB, pickB);
            }
            else
            {
                AddNode(pickA, endA);
                AddNode(pickB, endB);
            }
        }

        AddNode(po.SpawnPadTile.X, po.SpawnPadTile.Y);

        for (int i = 0; i < bridges.Count; i++)
        {
            AddNode(bridges[i].A % width, bridges[i].A / width);
            AddNode(bridges[i].B % width, bridges[i].B / width);
        }

        for (int i = 0; i < ferryArr.Length; i++)
        {
            AddNode(ferryArr[i].A.X, ferryArr[i].A.Y);
            AddNode(ferryArr[i].B.X, ferryArr[i].B.Y);
        }

        nodeTiles.Sort((a, b) =>
        {
            int cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        });

        var idAt = new int[count];
        for (int i = 0; i < count; i++)
            idAt[i] = -1;
        var nodes = new RouteNodeRecord[nodeTiles.Count];
        for (int i = 0; i < nodeTiles.Count; i++)
        {
            var t = nodeTiles[i];
            nodes[i] = new RouteNodeRecord(i, t);
            idAt[WorldGrid.Idx(t.X, t.Y, width)] = i;
        }

        var edges = new List<RouteEdgeRecord>();
        var edgeKeys = new HashSet<long>();

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
            if (!edgeKeys.Add(key)) return;
            edges.Add(new RouteEdgeRecord(from, to, length, surface));
        }

        for (int i = 0; i < nodeTiles.Count; i++)
        {
            var t = nodeTiles[i];
            for (int d = 0; d < 4; d++)
            {
                int cx = t.X + WorldGrid.Dx[d];
                int cy = t.Y + WorldGrid.Dy[d];
                int steps = 1;
                while (WorldGrid.InBounds(cx, cy, width, height))
                {
                    int ni = WorldGrid.Idx(cx, cy, width);
                    if (!streetMask[ni]) break;
                    int other = idAt[ni];
                    if (other >= 0)
                    {
                        if (other > i)
                            AddEdge(i, other, steps, SurfaceStreet);
                        break;
                    }

                    cx += WorldGrid.Dx[d];
                    cy += WorldGrid.Dy[d];
                    steps++;
                }
            }
        }

        for (int i = 0; i < bridges.Count; i++)
        {
            int ia = idAt[bridges[i].A];
            int ib = idAt[bridges[i].B];
            if (ia < 0 || ib < 0) continue;
            AddEdge(ia, ib, bridges[i].Gap, SurfaceBridge);
        }

        for (int i = 0; i < ferryArr.Length; i++)
        {
            int ia = idAt[WorldGrid.Idx(ferryArr[i].A.X, ferryArr[i].A.Y, width)];
            int ib = idAt[WorldGrid.Idx(ferryArr[i].B.X, ferryArr[i].B.Y, width)];
            if (ia < 0 || ib < 0) continue;
            int len = WorldGrid.Manhattan(ferryArr[i].A.X, ferryArr[i].A.Y, ferryArr[i].B.X, ferryArr[i].B.Y);
            AddEdge(ia, ib, len, SurfaceFerry);
        }

        int poId = WorldGrid.InBounds(po.SpawnPadTile.X, po.SpawnPadTile.Y, width, height)
            ? idAt[spawnPad]
            : -1;
        if (poId >= 0)
        {
            int best = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < nodeTiles.Count; i++)
            {
                if (i == poId) continue;
                int ni = WorldGrid.Idx(nodeTiles[i].X, nodeTiles[i].Y, width);
                if (!streetMask[ni]) continue;
                int d = WorldGrid.DistSq(po.SpawnPadTile.X, po.SpawnPadTile.Y, nodeTiles[i].X, nodeTiles[i].Y);
                if (d < bestD || (d == bestD && i < best))
                {
                    bestD = d;
                    best = i;
                }
            }

            if (best >= 0)
            {
                int len = WorldGrid.Manhattan(
                    po.SpawnPadTile.X,
                    po.SpawnPadTile.Y,
                    nodeTiles[best].X,
                    nodeTiles[best].Y);
                AddEdge(poId, best, len, SurfaceStreet);
            }
        }

        if (edges.Count == 0 && nodes.Length >= 2)
        {
            int len = WorldGrid.Manhattan(nodes[0].Tile.X, nodes[0].Tile.Y, nodes[1].Tile.X, nodes[1].Tile.Y);
            AddEdge(0, 1, len, SurfaceStreet);
        }

        return new ConnectivityResult(ferryArr, nodes, edges.ToArray());
    }
}
