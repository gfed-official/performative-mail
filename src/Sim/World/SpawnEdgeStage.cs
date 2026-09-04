using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

internal static class SpawnEdgeStage
{
    public const int MinChebyshevFromPo = 60;
    public const int MinChebyshevFromHouse = 20;

    public static SpawnEdgeRecord[] Place(
        RngStream spawns,
        short[] heights,
        bool[] walk,
        PostOfficeRecord po,
        HouseRecord[] houses,
        FerryLaneRecord[] ferries,
        int width,
        int height)
    {
        if (spawns is null) throw new ArgumentNullException(nameof(spawns));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (walk is null) throw new ArgumentNullException(nameof(walk));
        if (houses is null) throw new ArgumentNullException(nameof(houses));
        if (ferries is null) throw new ArgumentNullException(nameof(ferries));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        if (houses.Length == 0)
            return Array.Empty<SpawnEdgeRecord>();

        int count = width * height;
        int pcx = po.Tile.X + po.SizeTiles.X / 2;
        int pcy = po.Tile.Y + po.SizeTiles.Y / 2;
        int houseRadius = MinChebyshevFromHouse - 1;
        var tooClose = new bool[count];
        for (int h = 0; h < houses.Length; h++)
        {
            foreach (var tile in houses[h].Lot.Tiles())
            {
                int y0 = tile.Y - houseRadius;
                int y1 = tile.Y + houseRadius;
                int x0 = tile.X - houseRadius;
                int x1 = tile.X + houseRadius;
                for (int y = y0; y <= y1; y++)
                {
                    if ((uint)y >= (uint)height) continue;
                    int row = y * width;
                    for (int x = x0; x <= x1; x++)
                    {
                        if ((uint)x >= (uint)width) continue;
                        tooClose[row + x] = true;
                    }
                }
            }
        }

        var byDistrict = new Dictionary<byte, List<int>>();
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                if (!HeightmapStage.IsLand(heights[i])) continue;
                if (tooClose[i]) continue;
                if (WorldGrid.Chebyshev(x, y, pcx, pcy) < MinChebyshevFromPo) continue;
                if (!TouchesWater(heights, x, y, width, height)) continue;
                byte district = NearestHouseDistrict(houses, x, y);
                if (!byDistrict.TryGetValue(district, out var list))
                {
                    list = new List<int>();
                    byDistrict[district] = list;
                }

                list.Add(i);
            }
        }

        int spawn = WorldGrid.WalkStart(po, walk, width, height);
        var seen = new bool[count];
        var parent = new int[count];
        WorldGrid.FerryPairs(ferries, width, height, out int[] ferryFrom, out int[] ferryTo);
        WorldGrid.BfsWalk(walk, heights, width, height, spawn, seen, parent, ferryFrom, ferryTo);

        var edges = new List<SpawnEdgeRecord>();
        var districts = new byte[byDistrict.Count];
        int n = 0;
        foreach (var key in byDistrict.Keys)
            districts[n++] = key;
        Array.Sort(districts);
        for (int d = 0; d < districts.Length; d++)
        {
            byte district = districts[d];
            var list = byDistrict[district];
            list.Sort((a, b) =>
            {
                int da = WorldGrid.DistSq(a % width, a / width, pcx, pcy);
                int db = WorldGrid.DistSq(b % width, b / width, pcx, pcy);
                int cmp = db.CompareTo(da);
                if (cmp != 0) return cmp;
                int ya = a / width;
                int yb = b / width;
                cmp = ya.CompareTo(yb);
                if (cmp != 0) return cmp;
                return (a % width).CompareTo(b % width);
            });

            for (int i = 0; i < list.Count; i++)
            {
                int idx = list[i];
                if (!seen[idx]) continue;
                var path = WorldGrid.PathFromToStart(idx, spawn, parent, width);
                if (path is null) continue;
                edges.Add(new SpawnEdgeRecord(district, new TileCoord(idx % width, idx / width), path));
                break;
            }
        }

        edges.Sort((a, b) =>
        {
            int cmp = a.District.CompareTo(b.District);
            if (cmp != 0) return cmp;
            cmp = a.Tile.Y.CompareTo(b.Tile.Y);
            if (cmp != 0) return cmp;
            return a.Tile.X.CompareTo(b.Tile.X);
        });

        return edges.ToArray();
    }

    private static bool TouchesWater(short[] heights, int x, int y, int width, int height)
    {
        for (int d = 0; d < 4; d++)
        {
            int nx = x + WorldGrid.Dx[d];
            int ny = y + WorldGrid.Dy[d];
            if (!WorldGrid.InBounds(nx, ny, width, height)) continue;
            if (!HeightmapStage.IsLand(heights[WorldGrid.Idx(nx, ny, width)]))
                return true;
        }

        return false;
    }

    private static byte NearestHouseDistrict(HouseRecord[] houses, int x, int y)
    {
        int best = int.MaxValue;
        uint bestPacked = uint.MaxValue;
        byte district = houses[0].Address.District;
        for (int i = 0; i < houses.Length; i++)
        {
            var lot = houses[i].Lot;
            int lx = lot.X + lot.Width / 2;
            int ly = lot.Y + lot.Height / 2;
            int d = WorldGrid.DistSq(x, y, lx, ly);
            uint packed = houses[i].Address.Packed;
            if (d < best || (d == best && packed < bestPacked))
            {
                best = d;
                bestPacked = packed;
                district = houses[i].Address.District;
            }
        }

        return district;
    }
}
