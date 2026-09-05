using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public readonly record struct BeltItem(int ItemId, float MetresFromStart, MailKindId Kind);

public readonly record struct BeltJunctionPort(TileCoord Tile, Facing Facing);

public sealed class BeltJunction
{
    private readonly BeltJunctionPort[] _inputs;
    private readonly BeltJunctionPort[] _outputs;
    private readonly MailKindId?[] _filters = new MailKindId?[4];
    private readonly BeltSegment[] _inputSegments;
    private readonly BeltSegment[] _outputSegments;
    private readonly int[] _cursors;

    internal BeltJunction(
        TileCoord tile,
        Facing facing,
        bool isSplitter,
        BeltJunctionPort[] inputs,
        BeltSegment[] inputSegments,
        BeltJunctionPort[] outputs,
        BeltSegment[] outputSegments)
    {
        Tile = tile;
        Facing = facing;
        IsSplitter = isSplitter;
        _inputs = inputs;
        _outputs = outputs;
        _inputSegments = inputSegments;
        _outputSegments = outputSegments;
        _cursors = new int[BeltNetwork.LaneCount];
    }

    public TileCoord Tile { get; }

    public Facing Facing { get; }

    public bool IsSplitter { get; }

    public IReadOnlyList<BeltJunctionPort> Inputs => _inputs;

    public IReadOnlyList<BeltJunctionPort> Outputs => _outputs;

    internal void SetFilter(Facing outputFace, MailKindId? kind)
    {
        int i = (int)outputFace;
        if ((uint)i >= 4) return;
        _filters[i] = kind;
    }

    private bool Accepts(Facing outputFace, MailKindId kind)
    {
        int i = (int)outputFace;
        if ((uint)i >= 4) return true;
        MailKindId? want = _filters[i];
        return want is null || want.Value.Equals(kind);
    }

    internal void TransferLane(int lane)
    {
        if (IsSplitter)
            TransferSplit(lane);
        else
            TransferMerge(lane);
    }

    private void TransferSplit(int lane)
    {
        if (_inputSegments.Length == 0 || _outputSegments.Length == 0) return;
        var input = _inputSegments[0];
        if (!input.TryPeekHead(lane, out var item)) return;

        int n = _outputSegments.Length;
        int start = _cursors[lane];
        for (int k = 0; k < n; k++)
        {
            int i = (start + k) % n;
            if (!Accepts(_outputs[i].Facing, item.Kind)) continue;
            if (!_outputSegments[i].TryInsert(lane, item.ItemId, 0f, item.Kind)) continue;
            input.TryTakeHead(lane, out _);
            _cursors[lane] = (i + 1) % n;
            return;
        }
    }

    private void TransferMerge(int lane)
    {
        if (_inputSegments.Length == 0 || _outputSegments.Length == 0) return;
        var output = _outputSegments[0];
        int n = _inputSegments.Length;
        int start = _cursors[lane];
        for (int k = 0; k < n; k++)
        {
            int i = (start + k) % n;
            if (!_inputSegments[i].TryPeekHead(lane, out var item)) continue;
            if (!output.TryInsert(lane, item.ItemId, 0f, item.Kind)) continue;
            _inputSegments[i].TryTakeHead(lane, out _);
            _cursors[lane] = (i + 1) % n;
            return;
        }
    }
}

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
        => TryInsert(lane, itemId, metresFromStart, MailKinds.Letter);

    public bool TryInsert(int lane, int itemId, float metresFromStart, MailKindId kind)
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

        items.Add(new BeltItem(itemId, metresFromStart, kind));
        items.Sort(CompareHeadFirst);
        return true;
    }

    internal bool TryPeekHead(int lane, out BeltItem item)
    {
        item = default;
        if (lane != 0 && lane != 1) return false;
        var items = LaneList(lane);
        if (items.Count == 0) return false;
        if (items[0].MetresFromStart < LengthMetres) return false;
        item = items[0];
        return true;
    }

    internal bool TryTakeHead(int lane, out BeltItem item)
    {
        if (!TryPeekHead(lane, out item)) return false;
        LaneList(lane).RemoveAt(0);
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
            items[i] = new BeltItem(item.ItemId, next, item.Kind);
            ceiling = next - BeltNetwork.MinSpacingMetres;
        }
    }

    private static int CompareHeadFirst(BeltItem a, BeltItem b) =>
        b.MetresFromStart.CompareTo(a.MetresFromStart);
}

public sealed class BeltNetwork
{
    public const string BuildingId = "belt_mk1";
    public const string RampId = "belt_mk1_ramp";
    public const string ElevatedId = "belt_mk1_elevated";
    public const string SplitterId = "splitter";
    public const string MergerId = "merger";
    public const int JunctionWays = 3;
    public const float TileMetres = 2f;
    public const float Mk1MetresPerSecond = 2f;
    public const float MinSpacingMetres = 0.5f;
    public const int LaneCount = 2;

    private readonly List<BeltSegment> _segments = new List<BeltSegment>();
    private readonly List<BeltJunction> _junctions = new List<BeltJunction>();

    public IReadOnlyList<BeltSegment> Segments => _segments;

    public IReadOnlyList<BeltJunction> Junctions => _junctions;

    public void Compile(IReadOnlyList<ConstructRecord> constructs)
    {
        if (constructs is null) throw new ArgumentNullException(nameof(constructs));
        _segments.Clear();
        _junctions.Clear();

        var facingAt = new Dictionary<TileCoord, Facing>();
        for (int i = 0; i < constructs.Count; i++)
        {
            var row = constructs[i];
            if (!IsFamily(row.DefId))
                continue;
            facingAt[row.Tile] = row.Rotation;
            if (!string.Equals(row.DefId, RampId, StringComparison.Ordinal))
                continue;
            NextTile(row.Tile, row.Rotation, out var extra);
            facingAt[extra] = row.Rotation;
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
        CompileJunctions(constructs, facingAt);
    }

    public bool SetOutputFilter(TileCoord tile, Facing outputFace, MailKindId? kind)
    {
        for (int i = 0; i < _junctions.Count; i++)
        {
            var junction = _junctions[i];
            if (!junction.IsSplitter || !junction.Tile.Equals(tile)) continue;
            junction.SetFilter(outputFace, kind);
            return true;
        }

        return false;
    }

    public void Step(float dt)
    {
        if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt), dt, null);
        for (int i = 0; i < _segments.Count; i++)
            _segments[i].Step(dt);
        Transfer();
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

    private void CompileJunctions(
        IReadOnlyList<ConstructRecord> constructs,
        Dictionary<TileCoord, Facing> facingAt)
    {
        var rows = new List<ConstructRecord>();
        for (int i = 0; i < constructs.Count; i++)
        {
            var row = constructs[i];
            if (IsJunctionId(row.DefId))
                rows.Add(row);
        }

        rows.Sort((a, b) => CompareTiles(a.Tile, b.Tile));
        for (int i = 0; i < rows.Count; i++)
            _junctions.Add(BuildJunction(rows[i], facingAt));
    }

    private BeltJunction BuildJunction(ConstructRecord row, Dictionary<TileCoord, Facing> facingAt)
    {
        bool splitter = string.Equals(row.DefId, SplitterId, StringComparison.Ordinal);
        Facing facing = row.Rotation;
        var inputPorts = new List<BeltJunctionPort>(JunctionWays);
        var inputSegs = new List<BeltSegment>(JunctionWays);
        var outputPorts = new List<BeltJunctionPort>(JunctionWays);
        var outputSegs = new List<BeltSegment>(JunctionWays);

        if (splitter)
        {
            TryAttachInput(row.Tile, OppositeFace(facing), facing, facingAt, inputPorts, inputSegs);
            TryAttachOutput(row.Tile, facing, facingAt, outputPorts, outputSegs);
            TryAttachOutput(row.Tile, LeftOf(facing), facingAt, outputPorts, outputSegs);
            TryAttachOutput(row.Tile, RightOf(facing), facingAt, outputPorts, outputSegs);
        }
        else
        {
            TryAttachInput(row.Tile, LeftOf(facing), OppositeFace(LeftOf(facing)), facingAt, inputPorts, inputSegs);
            TryAttachInput(row.Tile, OppositeFace(facing), facing, facingAt, inputPorts, inputSegs);
            TryAttachInput(row.Tile, RightOf(facing), OppositeFace(RightOf(facing)), facingAt, inputPorts, inputSegs);
            TryAttachOutput(row.Tile, facing, facingAt, outputPorts, outputSegs);
        }

        return new BeltJunction(
            row.Tile,
            facing,
            splitter,
            inputPorts.ToArray(),
            inputSegs.ToArray(),
            outputPorts.ToArray(),
            outputSegs.ToArray());
    }

    private void TryAttachInput(
        TileCoord junction,
        Facing neighborDir,
        Facing travel,
        Dictionary<TileCoord, Facing> facingAt,
        List<BeltJunctionPort> ports,
        List<BeltSegment> segments)
    {
        NextTile(junction, neighborDir, out var neighbor);
        if (!facingAt.TryGetValue(neighbor, out Facing at) || at != travel) return;
        var segment = FindEnd(neighbor);
        if (segment is null) return;
        ports.Add(new BeltJunctionPort(neighbor, travel));
        segments.Add(segment);
    }

    private void TryAttachOutput(
        TileCoord junction,
        Facing travel,
        Dictionary<TileCoord, Facing> facingAt,
        List<BeltJunctionPort> ports,
        List<BeltSegment> segments)
    {
        NextTile(junction, travel, out var neighbor);
        if (!facingAt.TryGetValue(neighbor, out Facing at) || at != travel) return;
        var segment = FindStart(neighbor, travel);
        if (segment is null) return;
        ports.Add(new BeltJunctionPort(neighbor, travel));
        segments.Add(segment);
    }

    private BeltSegment? FindEnd(TileCoord tile)
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            if (segment.Tiles[segment.Tiles.Count - 1].Equals(tile))
                return segment;
        }

        return null;
    }

    private BeltSegment? FindStart(TileCoord tile, Facing travel)
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            if (segment.Tiles[0].Equals(tile) && segment.Facing == travel)
                return segment;
        }

        return null;
    }

    private void Transfer()
    {
        for (int i = 0; i < _junctions.Count; i++)
        {
            for (int lane = 0; lane < LaneCount; lane++)
                _junctions[i].TransferLane(lane);
        }
    }

    private static bool IsJunctionId(string defId) =>
        string.Equals(defId, SplitterId, StringComparison.Ordinal)
        || string.Equals(defId, MergerId, StringComparison.Ordinal);

    private static Facing LeftOf(Facing facing) => (Facing)(((int)facing + 3) & 3);

    private static Facing RightOf(Facing facing) => (Facing)(((int)facing + 1) & 3);

    private static Facing OppositeFace(Facing facing) => (Facing)(((int)facing + 2) & 3);

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

    private static bool IsFamily(string defId) =>
        string.Equals(defId, BuildingId, StringComparison.Ordinal)
        || string.Equals(defId, RampId, StringComparison.Ordinal)
        || string.Equals(defId, ElevatedId, StringComparison.Ordinal);

    private static void NextTile(TileCoord tile, Facing facing, out TileCoord next)
    {
        Delta(facing, out int dx, out int dy);
        next = new TileCoord(tile.X + dx, tile.Y + dy);
    }

    private static bool Opposite(Facing a, Facing b)
    {
        switch (a)
        {
            case Facing.North: return b == Facing.South;
            case Facing.East: return b == Facing.West;
            case Facing.South: return b == Facing.North;
            case Facing.West: return b == Facing.East;
            default:
                throw new ArgumentOutOfRangeException(nameof(a), a, null);
        }
    }

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
