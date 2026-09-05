using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public readonly record struct Capsule(
    int ItemId,
    MailKindId Kind,
    AddressId Address,
    TileCoord TargetOutlet,
    float MetresAlongPath);

public sealed class PipeNetwork
{
    public const string PipeId = "pipe";
    public const string InletId = "pipe_inlet";
    public const string OutletId = "pipe_outlet";
    public const float TileMetres = 2f;
    public const float MetresPerSecond = 5f;
    public const float MinSpacingMetres = 1f;

    private readonly HashSet<TileCoord> _nodes = new HashSet<TileCoord>();
    private readonly HashSet<TileCoord> _inletSet = new HashSet<TileCoord>();
    private readonly HashSet<TileCoord> _outletSet = new HashSet<TileCoord>();
    private readonly List<TileCoord> _inlets = new List<TileCoord>();
    private readonly List<TileCoord> _outlets = new List<TileCoord>();
    private readonly Dictionary<TileCoord, List<MappedFilter>> _filters =
        new Dictionary<TileCoord, List<MappedFilter>>();
    private readonly Dictionary<TileCoord, TileCoord> _defaults = new Dictionary<TileCoord, TileCoord>();
    private readonly List<InFlight> _moving = new List<InFlight>();
    private readonly Dictionary<TileCoord, List<Capsule>> _emitted =
        new Dictionary<TileCoord, List<Capsule>>();

    public IReadOnlyList<TileCoord> Inlets => _inlets;

    public IReadOnlyList<TileCoord> Outlets => _outlets;

    public IReadOnlyList<Capsule> Capsules
    {
        get
        {
            var rows = new Capsule[_moving.Count];
            for (int i = 0; i < _moving.Count; i++)
                rows[i] = _moving[i].Capsule;
            return rows;
        }
    }

    public void Compile(IReadOnlyList<ConstructRecord> constructs)
    {
        if (constructs is null) throw new ArgumentNullException(nameof(constructs));
        _nodes.Clear();
        _inletSet.Clear();
        _outletSet.Clear();
        _inlets.Clear();
        _outlets.Clear();
        _filters.Clear();
        _defaults.Clear();
        _moving.Clear();
        _emitted.Clear();

        for (int i = 0; i < constructs.Count; i++)
        {
            var row = constructs[i];
            if (!IsFamily(row.DefId)) continue;
            _nodes.Add(row.Tile);
            if (string.Equals(row.DefId, InletId, StringComparison.Ordinal))
            {
                _inletSet.Add(row.Tile);
                _inlets.Add(row.Tile);
            }
            else if (string.Equals(row.DefId, OutletId, StringComparison.Ordinal))
            {
                _outletSet.Add(row.Tile);
                _outlets.Add(row.Tile);
                _emitted[row.Tile] = new List<Capsule>();
            }
        }

        _inlets.Sort(CompareTiles);
        _outlets.Sort(CompareTiles);
    }

    public bool SetFilter(TileCoord inlet, AddressFilter filter, TileCoord outlet)
    {
        if (!_inletSet.Contains(inlet) || !_outletSet.Contains(outlet))
            return false;
        if (!_filters.TryGetValue(inlet, out var rules))
        {
            rules = new List<MappedFilter>();
            _filters[inlet] = rules;
        }

        rules.Add(new MappedFilter(filter, outlet));
        return true;
    }

    public bool SetDefaultOutlet(TileCoord inlet, TileCoord outlet)
    {
        if (!_inletSet.Contains(inlet) || !_outletSet.Contains(outlet))
            return false;
        _defaults[inlet] = outlet;
        return true;
    }

    public TileCoord Route(TileCoord inlet, in BeltItem item)
        => TryRoute(inlet, item, out var outlet) ? outlet : default;

    public bool TryRoute(TileCoord inlet, in BeltItem item, out TileCoord outlet)
    {
        outlet = default;
        if (!_inletSet.Contains(inlet)) return false;
        if (_filters.TryGetValue(inlet, out var rules))
        {
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (!rule.Filter.Matches(item)) continue;
                outlet = rule.Outlet;
                return true;
            }
        }

        if (!_defaults.TryGetValue(inlet, out outlet))
            return false;
        return true;
    }

    public bool TryAccept(TileCoord inlet, int itemId, MailKindId kind, AddressId address)
    {
        var item = new BeltItem(itemId, 0f, kind, address);
        if (!TryRoute(inlet, item, out var outlet))
            return false;
        if (!TryPathLength(inlet, outlet, out float pathLength))
            return false;
        if (BlockedAtStart(inlet, outlet))
            return false;

        _moving.Add(new InFlight(new Capsule(itemId, kind, address, outlet, 0f), inlet, pathLength));
        return true;
    }

    public IReadOnlyList<Capsule> Emitted(TileCoord outlet)
    {
        if (_emitted.TryGetValue(outlet, out var rows))
            return rows;
        return Array.Empty<Capsule>();
    }

    public void Step(float dt)
    {
        if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt), dt, null);
        for (int i = _moving.Count - 1; i >= 0; i--)
        {
            var row = _moving[i];
            row.ElapsedSeconds += dt;
            // speed * elapsed, not metres += speed * dt: 36 float adds undershoot 6 m.
            float next = (float)(MetresPerSecond * row.ElapsedSeconds);
            var updated = row.Capsule with { MetresAlongPath = next };
            if (next >= row.PathLength)
            {
                _emitted[updated.TargetOutlet].Add(updated);
                _moving.RemoveAt(i);
                continue;
            }

            row.Capsule = updated;
        }
    }

    public void StepTicks(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, null);
        float dt = (float)TickClock.TickDurationSeconds;
        for (int n = 0; n < ticks; n++)
            Step(dt);
    }

    private bool BlockedAtStart(TileCoord inlet, TileCoord outlet)
    {
        for (int i = 0; i < _moving.Count; i++)
        {
            var row = _moving[i];
            if (!row.Inlet.Equals(inlet) || !row.Capsule.TargetOutlet.Equals(outlet))
                continue;
            if (row.Capsule.MetresAlongPath < MinSpacingMetres)
                return true;
        }

        return false;
    }

    private bool TryPathLength(TileCoord start, TileCoord goal, out float length)
    {
        length = 0f;
        if (!_nodes.Contains(start) || !_nodes.Contains(goal))
            return false;
        if (start.Equals(goal))
            return true;

        var visited = new HashSet<TileCoord> { start };
        var parent = new Dictionary<TileCoord, TileCoord>();
        var queue = new Queue<TileCoord>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            if (at.Equals(goal))
            {
                int hops = 0;
                var walk = at;
                while (!walk.Equals(start))
                {
                    hops++;
                    walk = parent[walk];
                }

                length = hops * TileMetres;
                return true;
            }

            for (int d = 0; d < 4; d++)
            {
                var next = BeltNetwork.Next(at, (Facing)d);
                if (!_nodes.Contains(next) || !visited.Add(next)) continue;
                parent[next] = at;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static bool IsFamily(string defId) =>
        string.Equals(defId, PipeId, StringComparison.Ordinal)
        || string.Equals(defId, InletId, StringComparison.Ordinal)
        || string.Equals(defId, OutletId, StringComparison.Ordinal);

    private static int CompareTiles(TileCoord a, TileCoord b)
    {
        int byX = a.X.CompareTo(b.X);
        return byX != 0 ? byX : a.Y.CompareTo(b.Y);
    }

    private readonly record struct MappedFilter(AddressFilter Filter, TileCoord Outlet);

    private sealed class InFlight
    {
        public InFlight(Capsule capsule, TileCoord inlet, float pathLength)
        {
            Capsule = capsule;
            Inlet = inlet;
            PathLength = pathLength;
            ElapsedSeconds = 0d;
        }

        public Capsule Capsule;
        public TileCoord Inlet;
        public float PathLength;
        public double ElapsedSeconds;
    }
}
