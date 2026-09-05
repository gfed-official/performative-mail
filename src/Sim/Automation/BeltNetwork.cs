using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public readonly record struct BeltItem(int ItemId, float MetresFromStart);

public sealed class BeltSegment
{
    private readonly TileCoord[] _tiles;
    private readonly List<BeltItem> _lane0 = new List<BeltItem>();
    private readonly List<BeltItem> _lane1 = new List<BeltItem>();

    internal BeltSegment(Facing facing, TileCoord[] tiles, ulong runHash)
    {
        Facing = facing;
        _tiles = tiles;
        RunHash = runHash;
    }

    public Facing Facing { get; }

    public IReadOnlyList<TileCoord> Tiles => _tiles;

    public ulong RunHash { get; }

    public float LengthMetres => _tiles.Length * BeltNetwork.TileMetres;

    public IReadOnlyList<BeltItem> Lane(int index)
    {
        if (index == 0) return _lane0;
        if (index == 1) return _lane1;
        throw new ArgumentOutOfRangeException(nameof(index), index, null);
    }

    public bool TryInsert(int lane, int itemId, float metresFromStart)
    {
        if (lane != 0 && lane != 1) return false;
        if (metresFromStart < 0f || metresFromStart > LengthMetres) return false;

        var items = LaneList(lane);
        for (int i = 0; i < items.Count; i++)
        {
            float gap = items[i].MetresFromStart - metresFromStart;
            if (gap < 0f) gap = -gap;
            if (gap < BeltNetwork.MinSpacingMetres) return false;
        }

        items.Add(new BeltItem(itemId, metresFromStart));
        items.Sort(CompareHeadFirst);
        return true;
    }

    internal void Step(float dt)
    {
        StepLane(_lane0, dt);
        StepLane(_lane1, dt);
    }

    private List<BeltItem> LaneList(int lane) => lane == 0 ? _lane0 : _lane1;

    private void StepLane(List<BeltItem> items, float dt)
    {
        float ceiling = LengthMetres;
        float travel = BeltNetwork.Mk1MetresPerSecond * dt;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            float desired = item.MetresFromStart + travel;
            float next = desired < ceiling ? desired : ceiling;
            items[i] = new BeltItem(item.ItemId, next);
            ceiling = next - BeltNetwork.MinSpacingMetres;
        }
    }

    private static int CompareHeadFirst(BeltItem a, BeltItem b) =>
        b.MetresFromStart.CompareTo(a.MetresFromStart);
}

public sealed class BeltNetwork
{
    public const string BuildingId = "belt_mk1";
    public const float TileMetres = 2f;
    public const float Mk1MetresPerSecond = 2f;
    public const float MinSpacingMetres = 0.5f;
    public const int LaneCount = 2;

    private readonly List<BeltSegment> _segments = new List<BeltSegment>();

    public IReadOnlyList<BeltSegment> Segments => _segments;

    public void Compile(IReadOnlyList<ConstructRecord> constructs)
    {
        if (constructs is null) throw new ArgumentNullException(nameof(constructs));
        _segments.Clear();

        var facingAt = new Dictionary<TileCoord, Facing>();
        for (int i = 0; i < constructs.Count; i++)
        {
            var row = constructs[i];
            if (!string.Equals(row.DefId, BuildingId, StringComparison.Ordinal))
                continue;
            facingAt[row.Tile] = row.Rotation;
        }

        var incoming = new Dictionary<TileCoord, int>();
        foreach (var pair in facingAt)
        {
            NextTile(pair.Key, pair.Value, out var next);
            if (!facingAt.ContainsKey(next)) continue;
            incoming.TryGetValue(next, out int n);
            incoming[next] = n + 1;
        }

        var remaining = new HashSet<TileCoord>(facingAt.Keys);
        var sources = new List<TileCoord>();
        foreach (var tile in remaining)
        {
            incoming.TryGetValue(tile, out int n);
            if (n == 0) sources.Add(tile);
        }

        sources.Sort(CompareTiles);
        for (int i = 0; i < sources.Count; i++)
            EmitRun(sources[i], facingAt, incoming, remaining);

        while (remaining.Count > 0)
            EmitRun(First(remaining), facingAt, incoming, remaining);

        _segments.Sort(CompareRuns);
    }

    public void Step(float dt)
    {
        if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt), dt, null);
        for (int i = 0; i < _segments.Count; i++)
            _segments[i].Step(dt);
    }

    public void StepTicks(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, null);
        float dt = (float)TickClock.TickDurationSeconds;
        for (int n = 0; n < ticks; n++)
            Step(dt);
    }

    private void EmitRun(
        TileCoord start,
        Dictionary<TileCoord, Facing> facingAt,
        Dictionary<TileCoord, int> incoming,
        HashSet<TileCoord> remaining)
    {
        if (!remaining.Contains(start)) return;

        var run = new List<TileCoord>();
        var facings = new List<Facing>();
        TileCoord at = start;
        while (true)
        {
            remaining.Remove(at);
            Facing facing = facingAt[at];
            run.Add(at);
            facings.Add(facing);
            NextTile(at, facing, out var next);
            if (!remaining.Contains(next)) break;
            Facing nextFacing = facingAt[next];
            if (Opposite(facing, nextFacing)) break;
            incoming.TryGetValue(next, out int n);
            if (n > 1) break;
            at = next;
        }

        var tiles = run.ToArray();
        _segments.Add(new BeltSegment(facings[0], tiles, HashRun(tiles, facings)));
    }

    private static ulong HashRun(TileCoord[] tiles, List<Facing> facings)
    {
        ulong hash = Fnv.Offset64;
        hash = Fnv.MixUInt32(hash, (uint)tiles.Length);
        for (int i = 0; i < tiles.Length; i++)
        {
            hash = Fnv.Mix8(hash, (byte)facings[i]);
            hash = Fnv.MixUInt32(hash, unchecked((uint)tiles[i].X));
            hash = Fnv.MixUInt32(hash, unchecked((uint)tiles[i].Y));
        }

        return hash;
    }

    private static void NextTile(TileCoord tile, Facing facing, out TileCoord next)
    {
        Delta(facing, out int dx, out int dy);
        next = new TileCoord(tile.X + dx, tile.Y + dy);
    }

    private static bool Opposite(Facing a, Facing b) => ((int)a ^ (int)b) == 2;

    private static int CompareTiles(TileCoord a, TileCoord b)
    {
        int byX = a.X.CompareTo(b.X);
        if (byX != 0) return byX;
        return a.Y.CompareTo(b.Y);
    }

    private static void Delta(Facing facing, out int dx, out int dy)
    {
        switch (facing)
        {
            case Facing.North:
                dx = 0;
                dy = 1;
                return;
            case Facing.East:
                dx = 1;
                dy = 0;
                return;
            case Facing.South:
                dx = 0;
                dy = -1;
                return;
            case Facing.West:
                dx = -1;
                dy = 0;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
        }
    }

    private static TileCoord First(HashSet<TileCoord> tiles)
    {
        var best = default(TileCoord);
        bool any = false;
        foreach (var tile in tiles)
        {
            if (!any || tile.X < best.X || (tile.X == best.X && tile.Y < best.Y))
            {
                best = tile;
                any = true;
            }
        }

        if (!any)
            throw new InvalidOperationException("Belt run set was empty.");
        return best;
    }

    private static int CompareRuns(BeltSegment a, BeltSegment b)
    {
        int byFacing = a.Facing.CompareTo(b.Facing);
        if (byFacing != 0) return byFacing;
        int byX = a.Tiles[0].X.CompareTo(b.Tiles[0].X);
        if (byX != 0) return byX;
        return a.Tiles[0].Y.CompareTo(b.Tiles[0].Y);
    }
}
