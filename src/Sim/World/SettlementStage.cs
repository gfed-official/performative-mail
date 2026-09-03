using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

internal readonly struct SettlementResult
{
    public SettlementResult(
        PostOfficeRecord postOffice,
        StreetRecord[] streets,
        LotRecord[] lots,
        HouseRecord[] houses)
    {
        PostOffice = postOffice;
        Streets = streets;
        Lots = lots;
        Houses = houses;
    }

    public PostOfficeRecord PostOffice { get; }

    public StreetRecord[] Streets { get; }

    public LotRecord[] Lots { get; }

    public HouseRecord[] Houses { get; }
}

internal static class SettlementStage
{
    public const int MediumMinTiles = 260;
    public const int PoSize = 6;
    public const int StreetWidth = 2;
    public const int LotShort = 4;
    public const int LotLong = 6;
    public const int TargetHouses = 50;
    public const int DistrictSizeMin = 8;
    public const int DistrictSizeMax = 12;
    public const int DistrictSizeTarget = 10;
    public const int CrossSpacingMin = 10;
    public const int CrossSpacingMax = 14;
    public const int IntersectionRemovePermille = 200;
    public const int BendPermille = 150;
    public const int Tan10Num = 1763;
    public const int Tan10Den = 10000;
    public const int GapLimit = 2;

    private const byte Empty = 0;
    private const byte StreetOcc = 1;
    private const byte PoOcc = 2;
    private const byte LotOcc = 3;

    public static SettlementResult Generate(
        RngStream towns,
        RngStream addresses,
        short[] heights,
        bool[] buildable,
        int width,
        int height,
        int tileCm,
        string[] streetNames)
    {
        if (towns is null) throw new ArgumentNullException(nameof(towns));
        if (addresses is null) throw new ArgumentNullException(nameof(addresses));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (buildable is null) throw new ArgumentNullException(nameof(buildable));
        if (streetNames is null) throw new ArgumentNullException(nameof(streetNames));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));
        if (heights.Length != width * height)
            throw new ArgumentException("Height buffer must be width × height.", nameof(heights));
        if (buildable.Length != heights.Length)
            throw new ArgumentException("Buildable buffer must match height buffer.", nameof(buildable));
        if (streetNames.Length == 0)
            throw new ArgumentException("Street name list is empty.", nameof(streetNames));

        int count = width * height;
        var townFlat = new bool[count];
        BuildTownFlat(heights, buildable, townFlat, width, height, tileCm);

        var site = PickSite(townFlat, heights, width, height);
        var occup = new byte[count];
        var streetOf = new byte[count];
        var po = PlacePostOffice(site, townFlat, heights, occup, width, height);
        var streets = CarveStreets(towns, site, po, occup, streetOf, heights, width, height);
        AssignNames(addresses, streets, streetNames);
        var lots = PlaceLots(towns, streets, occup, streetOf, buildable, heights, width, height);
        var chosen = ChooseHouses(lots, po, TargetHouses);
        PartitionDistricts(chosen, streets);
        var houses = BuildHouses(chosen, streets, tileCm);
        var lotRecords = FreezeLots(lots, chosen);
        var streetRecords = FreezeStreets(streets);
        return new SettlementResult(po, streetRecords, lotRecords, houses);
    }

    internal static bool IsTownFlatPair(short a, short b, int tileCm)
    {
        int dh = a - b;
        if (dh < 0) dh = -dh;
        return (long)dh * Tan10Den <= (long)tileCm * Tan10Num;
    }

    private static void BuildTownFlat(
        short[] heights,
        bool[] buildable,
        bool[] townFlat,
        int width,
        int height,
        int tileCm)
    {
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                if (!buildable[i] || !HeightmapStage.IsLand(heights[i]))
                {
                    townFlat[i] = false;
                    continue;
                }

                bool flat = true;
                if (x > 0 && !IsTownFlatPair(heights[i], heights[row + (x - 1)], tileCm)) flat = false;
                if (flat && x + 1 < width && !IsTownFlatPair(heights[i], heights[row + (x + 1)], tileCm)) flat = false;
                if (flat && y > 0 && !IsTownFlatPair(heights[i], heights[(y - 1) * width + x], tileCm)) flat = false;
                if (flat && y + 1 < height && !IsTownFlatPair(heights[i], heights[(y + 1) * width + x], tileCm)) flat = false;
                townFlat[i] = flat;
            }
        }
    }

    private readonly struct Site
    {
        public Site(int cx, int cy, int minX, int minY, int maxX, int maxY, int size)
        {
            Cx = cx;
            Cy = cy;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Size = size;
        }

        public int Cx { get; }
        public int Cy { get; }
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }
        public int Size { get; }

        public bool EastWest => (MaxX - MinX) >= (MaxY - MinY);
    }

    private static Site PickSite(bool[] townFlat, short[] heights, int width, int height)
    {
        int count = width * height;
        var seen = new byte[count];
        var stack = new int[count];
        var seaDist = SeaDistance(heights, width, height);

        int bestScore = int.MinValue;
        Site best = new Site(width / 2, height / 2, 0, 0, width - 1, height - 1, 0);

        for (int start = 0; start < count; start++)
        {
            if (!townFlat[start] || seen[start] != 0) continue;
            int top = 0;
            stack[top++] = start;
            seen[start] = 1;
            long sumX = 0;
            long sumY = 0;
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            int size = 0;
            int reliefMin = short.MaxValue;
            int reliefMax = short.MinValue;
            int coast = int.MaxValue;

            while (top > 0)
            {
                int i = stack[--top];
                int x = i % width;
                int y = i / width;
                sumX += x;
                sumY += y;
                size++;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                short h = heights[i];
                if (h < reliefMin) reliefMin = h;
                if (h > reliefMax) reliefMax = h;
                int d = seaDist[i];
                if (d < coast) coast = d;

                TryPush(x - 1, y);
                TryPush(x + 1, y);
                TryPush(x, y - 1);
                TryPush(x, y + 1);

                void TryPush(int nx, int ny)
                {
                    if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) return;
                    int ni = ny * width + nx;
                    if (!townFlat[ni] || seen[ni] != 0) return;
                    seen[ni] = 1;
                    stack[top++] = ni;
                }
            }

            if (size < MediumMinTiles && best.Size >= MediumMinTiles) continue;
            int relief = reliefMax - reliefMin;
            int score = size - relief * 8 - coast * 4;
            bool prefer = size >= MediumMinTiles && best.Size < MediumMinTiles;
            if (!prefer && size < MediumMinTiles && best.Size >= MediumMinTiles) continue;
            if (prefer || score > bestScore || (score == bestScore && start < best.Cx + best.Cy * width))
            {
                bestScore = score;
                best = new Site((int)(sumX / size), (int)(sumY / size), minX, minY, maxX, maxY, size);
            }
        }

        return best;
    }

    private static int[] SeaDistance(short[] heights, int width, int height)
    {
        int count = width * height;
        var dist = new int[count];
        var q = new int[count];
        int head = 0;
        int tail = 0;
        for (int i = 0; i < count; i++)
        {
            if (HeightmapStage.IsLand(heights[i]))
            {
                dist[i] = int.MaxValue;
            }
            else
            {
                dist[i] = 0;
                q[tail++] = i;
            }
        }

        while (head < tail)
        {
            int i = q[head++];
            int x = i % width;
            int y = i / width;
            int nd = dist[i] + 1;
            Relax(x - 1, y);
            Relax(x + 1, y);
            Relax(x, y - 1);
            Relax(x, y + 1);

            void Relax(int nx, int ny)
            {
                if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) return;
                int ni = ny * width + nx;
                if (nd >= dist[ni]) return;
                dist[ni] = nd;
                q[tail++] = ni;
            }
        }

        return dist;
    }

    private static PostOfficeRecord PlacePostOffice(
        Site site,
        bool[] townFlat,
        short[] heights,
        byte[] occup,
        int width,
        int height)
    {
        int bestX = site.Cx - PoSize / 2;
        int bestY = site.Cy - PoSize / 2;
        int bestDist = int.MaxValue;
        int bestRange = int.MaxValue;
        bool found = false;

        int radius = Math.Max(width, height);
        for (int r = 0; r <= radius; r++)
        {
            int x0 = site.Cx - PoSize / 2 - r;
            int x1 = site.Cx - PoSize / 2 + r;
            int y0 = site.Cy - PoSize / 2 - r;
            int y1 = site.Cy - PoSize / 2 + r;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (r != 0 && x != x0 && x != x1 && y != y0 && y != y1) continue;
                    if (!PatchOk(x, y, out int range)) continue;
                    int dx = x + PoSize / 2 - site.Cx;
                    int dy = y + PoSize / 2 - site.Cy;
                    int dist = dx * dx + dy * dy;
                    if (!found || range < bestRange || (range == bestRange && dist < bestDist) ||
                        (range == bestRange && dist == bestDist && (y < bestY || (y == bestY && x < bestX))))
                    {
                        found = true;
                        bestX = x;
                        bestY = y;
                        bestDist = dist;
                        bestRange = range;
                    }
                }
            }

            if (found && r >= 2) break;
        }

        if (!found)
        {
            bestX = Clamp(site.Cx - PoSize / 2, 0, width - PoSize);
            bestY = Clamp(site.Cy - PoSize / 2, 0, height - PoSize);
        }

        var tile = new TileCoord(bestX, bestY);
        var size = new TileCoord(PoSize, PoSize);
        var spawn = new TileCoord(bestX + 2, bestY + 2);
        var intake = new TileCoord(bestX + 5, bestY + 2);
        PaintRect(occup, width, height, new TileRect(bestX, bestY, PoSize, PoSize), PoOcc);
        return new PostOfficeRecord(tile, size, spawn, intake, Facing.East);

        bool PatchOk(int ox, int oy, out int range)
        {
            range = 0;
            if (ox < 0 || oy < 0 || ox + PoSize > width || oy + PoSize > height) return false;
            short lo = short.MaxValue;
            short hi = short.MinValue;
            for (int y = oy; y < oy + PoSize; y++)
            {
                int row = y * width;
                for (int x = ox; x < ox + PoSize; x++)
                {
                    int i = row + x;
                    if (!townFlat[i]) return false;
                    short h = heights[i];
                    if (h < lo) lo = h;
                    if (h > hi) hi = h;
                }
            }

            range = hi - lo;
            return true;
        }
    }

    private sealed class StreetBuild
    {
        public StreetBuild(byte id, bool eastWest)
        {
            Id = id;
            EastWest = eastWest;
            Tiles = new List<TileCoord>();
        }

        public byte Id { get; }
        public bool EastWest { get; }
        public string Name { get; set; } = "";
        public byte District { get; set; }
        public List<TileCoord> Tiles { get; }
    }

    private static List<StreetBuild> CarveStreets(
        RngStream towns,
        Site site,
        PostOfficeRecord po,
        byte[] occup,
        byte[] streetOf,
        short[] heights,
        int width,
        int height)
    {
        var streets = new List<StreetBuild>();
        bool eastWest = site.EastWest;
        byte nextId = 1;

        int mainA0;
        if (eastWest)
        {
            int south = po.Tile.Y - StreetWidth;
            int north = po.Tile.Y + PoSize;
            bool southOk = south >= 0;
            bool northOk = north + 1 < height;
            if (southOk && northOk)
                mainA0 = Math.Abs(site.Cy - south) <= Math.Abs(site.Cy - north) ? south : north;
            else if (southOk) mainA0 = south;
            else if (northOk) mainA0 = north;
            else mainA0 = Clamp(site.Cy, 0, height - StreetWidth);
        }
        else
        {
            int west = po.Tile.X - StreetWidth;
            int east = po.Tile.X + PoSize;
            bool westOk = west >= 0;
            bool eastOk = east + 1 < width;
            if (westOk && eastOk)
                mainA0 = Math.Abs(site.Cx - west) <= Math.Abs(site.Cx - east) ? west : east;
            else if (westOk) mainA0 = west;
            else if (eastOk) mainA0 = east;
            else mainA0 = Clamp(site.Cx, 0, width - StreetWidth);
        }

        var main = new StreetBuild(nextId++, eastWest);
        if (eastWest)
            GrowEastWest(main, mainA0, site.Cx, occup, streetOf, heights, width, height);
        else
            GrowNorthSouth(main, mainA0, site.Cy, occup, streetOf, heights, width, height);
        if (main.Tiles.Count > 0)
            streets.Add(main);

        int spacing = CrossSpacingMin + (int)towns.NextBounded((uint)(CrossSpacingMax - CrossSpacingMin + 1));
        int origin = eastWest ? po.Tile.X + PoSize / 2 : po.Tile.Y + PoSize / 2;
        var axes = UniqueAxis(main, eastWest);
        bool firstCross = true;
        for (int n = 0; n < axes.Count; n++)
        {
            int pos = axes[n];
            int delta = pos - origin;
            if (delta < 0) delta = -delta;
            if (delta % spacing != 0) continue;
            bool drop = !firstCross && towns.NextBounded(1000) < IntersectionRemovePermille;
            firstCross = false;
            if (drop) continue;
            int shifted = pos;
            if (towns.NextBounded(1000) < BendPermille)
            {
                int bent = pos + 1;
                if (eastWest ? bent + 1 < width : bent + 1 < height)
                    shifted = bent;
            }

            var cross = new StreetBuild(nextId++, !eastWest);
            if (eastWest)
                GrowNorthSouth(cross, shifted, mainA0, occup, streetOf, heights, width, height);
            else
                GrowEastWest(cross, shifted, mainA0, occup, streetOf, heights, width, height);
            if (cross.Tiles.Count > 0)
                streets.Add(cross);
            if (nextId == 0) break;
        }

        return streets;
    }

    private static List<int> UniqueAxis(StreetBuild street, bool eastWest)
    {
        var set = new SortedSet<int>();
        for (int i = 0; i < street.Tiles.Count; i++)
        {
            var t = street.Tiles[i];
            set.Add(eastWest ? t.X : t.Y);
        }

        return new List<int>(set);
    }

    private static void GrowEastWest(
        StreetBuild street,
        int y0,
        int startX,
        byte[] occup,
        byte[] streetOf,
        short[] heights,
        int width,
        int height)
    {
        if (y0 < 0 || y0 + 1 >= height) return;
        PaintRun(startX, -1);
        PaintRun(startX + 1, 1);

        void PaintRun(int from, int step)
        {
            int fail = 0;
            for (int x = from; (uint)x < (uint)width; x += step)
            {
                if (TryPaint(x, y0) && TryPaint(x, y0 + 1))
                {
                    fail = 0;
                }
                else
                {
                    fail++;
                    if (fail >= GapLimit) break;
                }
            }
        }

        bool TryPaint(int x, int y)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
            int i = y * width + x;
            if (!HeightmapStage.IsLand(heights[i])) return false;
            if (occup[i] == PoOcc || occup[i] == LotOcc) return false;
            if (occup[i] == StreetOcc && streetOf[i] != street.Id && streetOf[i] != 0)
            {
                street.Tiles.Add(new TileCoord(x, y));
                return true;
            }

            occup[i] = StreetOcc;
            streetOf[i] = street.Id;
            street.Tiles.Add(new TileCoord(x, y));
            return true;
        }
    }

    private static void GrowNorthSouth(
        StreetBuild street,
        int x0,
        int startY,
        byte[] occup,
        byte[] streetOf,
        short[] heights,
        int width,
        int height)
    {
        if (x0 < 0 || x0 + 1 >= width) return;
        PaintRun(startY, -1);
        PaintRun(startY + 1, 1);

        void PaintRun(int from, int step)
        {
            int fail = 0;
            for (int y = from; (uint)y < (uint)height; y += step)
            {
                if (TryPaint(x0, y) && TryPaint(x0 + 1, y))
                {
                    fail = 0;
                }
                else
                {
                    fail++;
                    if (fail >= GapLimit) break;
                }
            }
        }

        bool TryPaint(int x, int y)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
            int i = y * width + x;
            if (!HeightmapStage.IsLand(heights[i])) return false;
            if (occup[i] == PoOcc || occup[i] == LotOcc) return false;
            if (occup[i] == StreetOcc && streetOf[i] != street.Id && streetOf[i] != 0)
            {
                street.Tiles.Add(new TileCoord(x, y));
                return true;
            }

            occup[i] = StreetOcc;
            streetOf[i] = street.Id;
            street.Tiles.Add(new TileCoord(x, y));
            return true;
        }
    }

    private static void AssignNames(RngStream addresses, List<StreetBuild> streets, string[] streetNames)
    {
        if (streets.Count > streetNames.Length)
            throw new InvalidOperationException("Not enough street names for the generated graph.");

        var order = new int[streetNames.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = (int)addresses.NextBounded((uint)(i + 1));
            int tmp = order[i];
            order[i] = order[j];
            order[j] = tmp;
        }

        for (int i = 0; i < streets.Count; i++)
            streets[i].Name = streetNames[order[i]];
    }

    private sealed class LotBuild
    {
        public LotBuild(int id, byte streetId, TileRect footprint, bool residential, bool left, int order)
        {
            Id = id;
            StreetId = streetId;
            Footprint = footprint;
            Residential = residential;
            Left = left;
            Order = order;
        }

        public int Id { get; }
        public byte StreetId { get; }
        public byte District { get; set; }
        public byte Number { get; set; }
        public TileRect Footprint { get; }
        public bool Residential { get; set; }
        public bool Left { get; }
        public int Order { get; }
        public bool Chosen { get; set; }
    }

    private static List<LotBuild> PlaceLots(
        RngStream towns,
        List<StreetBuild> streets,
        byte[] occup,
        byte[] streetOf,
        bool[] buildable,
        short[] heights,
        int width,
        int height)
    {
        var lots = new List<LotBuild>();
        int nextId = 1;
        for (int s = 0; s < streets.Count; s++)
        {
            var street = streets[s];
            if (street.EastWest)
                PlaceAlongEastWest(street);
            else
                PlaceAlongNorthSouth(street);
        }

        return lots;

        void PlaceAlongEastWest(StreetBuild street)
        {
            var xs = UniqueAxis(street, true);
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            BoundsY(street, out minY, out maxY);
            int order = 0;
            for (int i = 0; i + LotShort <= xs.Count; )
            {
                if (!Consecutive(xs, i, LotShort))
                {
                    i++;
                    continue;
                }

                int x = xs[i];
                int depth = towns.NextBounded(2) == 0 ? LotShort : LotLong;
                TrySide(x, maxY + 1, depth, true);
                TrySide(x, minY - depth, depth, false);
                i += LotShort;
                order++;
            }

            void TrySide(int x, int y, int depth, bool north)
            {
                var rect = new TileRect(x, y, LotShort, depth);
                if (!CanLot(rect, occup, streetOf, buildable, heights, width, height, street.Id, out bool corner))
                    return;
                PaintRect(occup, width, height, rect, LotOcc);
                lots.Add(new LotBuild(nextId++, street.Id, rect, !corner, north, order));
            }
        }

        void PlaceAlongNorthSouth(StreetBuild street)
        {
            var ys = UniqueAxis(street, false);
            BoundsX(street, out int minX, out int maxX);
            int order = 0;
            for (int i = 0; i + LotShort <= ys.Count; )
            {
                if (!Consecutive(ys, i, LotShort))
                {
                    i++;
                    continue;
                }

                int y = ys[i];
                int depth = towns.NextBounded(2) == 0 ? LotShort : LotLong;
                TrySide(maxX + 1, y, depth, false);
                TrySide(minX - depth, y, depth, true);
                i += LotShort;
                order++;
            }

            void TrySide(int x, int y, int depth, bool west)
            {
                var rect = new TileRect(x, y, depth, LotShort);
                if (!CanLot(rect, occup, streetOf, buildable, heights, width, height, street.Id, out bool corner))
                    return;
                PaintRect(occup, width, height, rect, LotOcc);
                lots.Add(new LotBuild(nextId++, street.Id, rect, !corner, west, order));
            }
        }

        void BoundsY(StreetBuild street, out int minY, out int maxY)
        {
            minY = int.MaxValue;
            maxY = int.MinValue;
            for (int i = 0; i < street.Tiles.Count; i++)
            {
                int y = street.Tiles[i].Y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        void BoundsX(StreetBuild street, out int minX, out int maxX)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            for (int i = 0; i < street.Tiles.Count; i++)
            {
                int x = street.Tiles[i].X;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }
        }
    }

    private static bool Consecutive(List<int> values, int start, int length)
    {
        int expected = values[start];
        for (int k = 0; k < length; k++)
        {
            if (start + k >= values.Count || values[start + k] != expected + k)
                return false;
        }

        return true;
    }

    private static bool CanLot(
        TileRect rect,
        byte[] occup,
        byte[] streetOf,
        bool[] buildable,
        short[] heights,
        int width,
        int height,
        byte streetId,
        out bool corner)
    {
        corner = false;
        if (rect.Width <= 0 || rect.Height <= 0) return false;
        if (rect.X < 0 || rect.Y < 0 || rect.MaxX > width || rect.MaxY > height) return false;

        var other = new HashSet<byte>();
        for (int y = rect.Y; y < rect.MaxY; y++)
        {
            int row = y * width;
            for (int x = rect.X; x < rect.MaxX; x++)
            {
                int i = row + x;
                if (!HeightmapStage.IsLand(heights[i]) || !buildable[i]) return false;
                if (occup[i] != Empty) return false;
                Probe(x - 1, y);
                Probe(x + 1, y);
                Probe(x, y - 1);
                Probe(x, y + 1);
            }
        }

        other.Remove(streetId);
        other.Remove(0);
        corner = other.Count > 0;
        return true;

        void Probe(int x, int y)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
            int i = y * width + x;
            if (occup[i] == StreetOcc)
                other.Add(streetOf[i]);
        }
    }

    private static List<LotBuild> ChooseHouses(List<LotBuild> lots, PostOfficeRecord po, int target)
    {
        var candidates = new List<LotBuild>();
        for (int i = 0; i < lots.Count; i++)
        {
            if (lots[i].Residential)
                candidates.Add(lots[i]);
        }

        int pcx = po.Tile.X + PoSize / 2;
        int pcy = po.Tile.Y + PoSize / 2;
        candidates.Sort((a, b) =>
        {
            int cmp = DistSq(a).CompareTo(DistSq(b));
            if (cmp != 0) return cmp;
            cmp = a.StreetId.CompareTo(b.StreetId);
            if (cmp != 0) return cmp;
            cmp = a.Footprint.X.CompareTo(b.Footprint.X);
            if (cmp != 0) return cmp;
            return a.Footprint.Y.CompareTo(b.Footprint.Y);
        });

        int take = Math.Min(target, candidates.Count);
        var chosen = new List<LotBuild>(take);
        for (int i = 0; i < take; i++)
        {
            candidates[i].Chosen = true;
            chosen.Add(candidates[i]);
        }

        return chosen;

        int DistSq(LotBuild lot)
        {
            int lx = lot.Footprint.X + lot.Footprint.Width / 2;
            int ly = lot.Footprint.Y + lot.Footprint.Height / 2;
            int dx = lx - pcx;
            int dy = ly - pcy;
            return dx * dx + dy * dy;
        }
    }

    private static void PartitionDistricts(List<LotBuild> houses, List<StreetBuild> streets)
    {
        NumberLots(houses, streets);

        int i = 0;
        byte district = 1;
        while (i < houses.Count)
        {
            int remaining = houses.Count - i;
            int take;
            if (remaining <= DistrictSizeMax)
            {
                take = remaining;
            }
            else
            {
                take = SnapDistrict(houses, i, remaining);
            }

            for (int k = 0; k < take; k++)
                houses[i + k].District = district;
            i += take;
            if (district < 255) district++;
        }

        for (int s = 0; s < streets.Count; s++)
        {
            var counts = new int[256];
            int best = 1;
            int bestN = 0;
            for (int h = 0; h < houses.Count; h++)
            {
                if (houses[h].StreetId != streets[s].Id) continue;
                int d = houses[h].District;
                counts[d]++;
                if (counts[d] > bestN)
                {
                    bestN = counts[d];
                    best = d;
                }
            }

            streets[s].District = (byte)(bestN == 0 ? 1 : best);
        }
    }

    private static int SnapDistrict(List<LotBuild> houses, int start, int remaining)
    {
        int take = DistrictSizeTarget;
        int acc = 0;
        byte street = houses[start].StreetId;
        for (int i = start; i < houses.Count && acc < DistrictSizeMax; i++)
        {
            if (houses[i].StreetId != street)
            {
                if (acc >= DistrictSizeMin && acc <= DistrictSizeMax)
                    take = acc;
                street = houses[i].StreetId;
                if (acc >= DistrictSizeMin)
                    break;
            }

            acc++;
        }

        if (acc >= DistrictSizeMin && acc <= DistrictSizeMax)
            take = acc;

        int after = remaining - take;
        if (after > 0 && after < DistrictSizeMin)
            take = remaining - DistrictSizeMin;
        if (take < DistrictSizeMin) take = DistrictSizeMin;
        if (take > DistrictSizeMax) take = DistrictSizeMax;
        if (take > remaining) take = remaining;
        return take;
    }

    private static void NumberLots(List<LotBuild> houses, List<StreetBuild> streets)
    {
        var byStreet = new Dictionary<byte, List<LotBuild>>();
        for (int i = 0; i < houses.Count; i++)
        {
            var lot = houses[i];
            if (!byStreet.TryGetValue(lot.StreetId, out var list))
            {
                list = new List<LotBuild>();
                byStreet[lot.StreetId] = list;
            }

            list.Add(lot);
        }

        for (int s = 0; s < streets.Count; s++)
        {
            var street = streets[s];
            if (!byStreet.TryGetValue(street.Id, out var list) || list.Count == 0)
                continue;

            bool walkPositive = WalkPositive(street);
            list.Sort((a, b) =>
            {
                int ax = street.EastWest ? a.Footprint.X : a.Footprint.Y;
                int bx = street.EastWest ? b.Footprint.X : b.Footprint.Y;
                return walkPositive ? ax.CompareTo(bx) : bx.CompareTo(ax);
            });

            byte odd = 1;
            byte even = 2;
            for (int i = 0; i < list.Count; i++)
            {
                bool left = IsLeft(list[i], street, walkPositive);
                if (left)
                {
                    list[i].Number = odd;
                    if (odd < 253) odd += 2;
                }
                else
                {
                    list[i].Number = even;
                    if (even < 254) even += 2;
                }
            }
        }
    }

    private static bool WalkPositive(StreetBuild street)
    {
        int min = int.MaxValue;
        int max = int.MinValue;
        for (int i = 0; i < street.Tiles.Count; i++)
        {
            int v = street.EastWest ? street.Tiles[i].X : street.Tiles[i].Y;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        int mid = 0;
        if (street.Tiles.Count > 0)
        {
            long sum = 0;
            for (int i = 0; i < street.Tiles.Count; i++)
                sum += street.EastWest ? street.Tiles[i].X : street.Tiles[i].Y;
            mid = (int)(sum / street.Tiles.Count);
        }

        int dMin = mid - min;
        if (dMin < 0) dMin = -dMin;
        int dMax = max - mid;
        if (dMax < 0) dMax = -dMax;
        return dMax >= dMin;
    }

    private static bool IsLeft(LotBuild lot, StreetBuild street, bool walkPositive)
    {
        if (street.EastWest)
        {
            bool north = lot.Footprint.Y >= StreetMax(street, true);
            return walkPositive ? north : !north;
        }

        bool west = lot.Footprint.MaxX <= StreetMin(street, false);
        return walkPositive ? west : !west;
    }

    private static int StreetMin(StreetBuild street, bool useY)
    {
        int min = int.MaxValue;
        for (int i = 0; i < street.Tiles.Count; i++)
        {
            int v = useY ? street.Tiles[i].Y : street.Tiles[i].X;
            if (v < min) min = v;
        }

        return min;
    }

    private static int StreetMax(StreetBuild street, bool useY)
    {
        int max = int.MinValue;
        for (int i = 0; i < street.Tiles.Count; i++)
        {
            int v = useY ? street.Tiles[i].Y : street.Tiles[i].X;
            if (v > max) max = v;
        }

        return max;
    }

    private static HouseRecord[] BuildHouses(List<LotBuild> chosen, List<StreetBuild> streets, int tileCm)
    {
        var byId = new Dictionary<byte, StreetBuild>(streets.Count);
        for (int i = 0; i < streets.Count; i++)
            byId[streets[i].Id] = streets[i];

        var houses = new HouseRecord[chosen.Count];
        for (int i = 0; i < chosen.Count; i++)
        {
            var lot = chosen[i];
            var street = byId[lot.StreetId];
            var address = new AddressId(lot.District, lot.StreetId, lot.Number, 0);
            var mailbox = MailboxOnEdge(lot, street, tileCm);
            houses[i] = new HouseRecord(
                address,
                new TileCoord(lot.Footprint.X, lot.Footprint.Y),
                new TileCoord(lot.Footprint.Width, lot.Footprint.Height),
                mailbox);
        }

        Array.Sort(houses, (a, b) => a.Address.Packed.CompareTo(b.Address.Packed));
        return houses;
    }

    private static MailboxPose MailboxOnEdge(LotBuild lot, StreetBuild street, int tileCm)
    {
        int x;
        int y;
        int yaw;
        if (street.EastWest)
        {
            x = (lot.Footprint.X + 1) * tileCm;
            bool north = lot.Footprint.Y >= StreetMax(street, true);
            if (north)
            {
                y = lot.Footprint.Y * tileCm;
                yaw = 180;
            }
            else
            {
                y = lot.Footprint.MaxY * tileCm;
                yaw = 0;
            }
        }
        else
        {
            y = (lot.Footprint.Y + 1) * tileCm;
            bool east = lot.Footprint.X >= StreetMax(street, false);
            if (east)
            {
                x = lot.Footprint.X * tileCm;
                yaw = 270;
            }
            else
            {
                x = lot.Footprint.MaxX * tileCm;
                yaw = 90;
            }
        }

        return new MailboxPose(x, y, 0, yaw);
    }

    private static LotRecord[] FreezeLots(List<LotBuild> lots, List<LotBuild> chosen)
    {
        var chosenIds = new HashSet<int>(chosen.Count);
        for (int i = 0; i < chosen.Count; i++)
            chosenIds.Add(chosen[i].Id);

        var records = new LotRecord[lots.Count];
        for (int i = 0; i < lots.Count; i++)
        {
            var lot = lots[i];
            bool residential = lot.Residential && chosenIds.Contains(lot.Id);
            records[i] = new LotRecord(lot.Id, lot.StreetId, lot.District, lot.Footprint, residential);
        }

        return records;
    }

    private static StreetRecord[] FreezeStreets(List<StreetBuild> streets)
    {
        var records = new StreetRecord[streets.Count];
        for (int i = 0; i < streets.Count; i++)
        {
            var s = streets[i];
            records[i] = new StreetRecord(s.Id, s.Name, s.District, s.Tiles.ToArray());
        }

        return records;
    }

    private static void PaintRect(byte[] occup, int width, int height, TileRect rect, byte value)
    {
        for (int y = rect.Y; y < rect.MaxY; y++)
        {
            if ((uint)y >= (uint)height) continue;
            int row = y * width;
            for (int x = rect.X; x < rect.MaxX; x++)
            {
                if ((uint)x >= (uint)width) continue;
                occup[row + x] = value;
            }
        }
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
