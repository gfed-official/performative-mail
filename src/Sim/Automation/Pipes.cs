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
    public const string JunctionId = "pipe_junction";
    public const string UndergroundId = "pipe_underground";
    public const float TileMetres = 2f;
    public const float MetresPerSecond = 5f;
    public const float MinSpacingMetres = 1f;

    private readonly HashSet<TileCoord> _nodes = new HashSet<TileCoord>();
    private readonly HashSet<TileCoord> _climbs = new HashSet<TileCoord>();
    private readonly HashSet<TileCoord> _blocked = new HashSet<TileCoord>();
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
        _climbs.Clear();
        _blocked.Clear();
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
            if (!IsFamily(row.DefId))
            {
                _climbs.Add(row.Tile);
                continue;
            }

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
                if (_blocked.Contains(rule.Outlet)) continue;
                outlet = rule.Outlet;
                return true;
            }
        }

        if (!_defaults.TryGetValue(inlet, out outlet) || _blocked.Contains(outlet))
        {
            outlet = default;
            return false;
        }

        return true;
    }

    public bool SetOutletBlocked(TileCoord outlet, bool blocked)
    {
        if (!_outletSet.Contains(outlet))
            return false;
        if (blocked)
            _blocked.Add(outlet);
        else
            _blocked.Remove(outlet);
        return true;
    }

    public bool TryAccept(TileCoord inlet, int itemId, MailKindId kind, AddressId address)
    {
        var item = new BeltItem(itemId, 0f, kind, address);
        if (!TryRoute(inlet, item, out var outlet))
            return false;
        if (!TryPathHops(inlet, outlet, out int hops))
            return false;
        if (BlockedAtStart(inlet))
            return false;

        _moving.Add(new InFlight(new Capsule(itemId, kind, address, outlet, 0f), inlet, hops));
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
        if (dt == 0f) return;
        int ticks = (int)Math.Round(dt * TickClock.TickHz);
        if (ticks > 0)
            AdvanceTicks(ticks);
    }

    public void StepTicks(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, null);
        AdvanceTicks(ticks);
    }

    private bool BlockedAtStart(TileCoord inlet)
    {
        for (int i = 0; i < _moving.Count; i++)
        {
            var row = _moving[i];
            if (!row.Inlet.Equals(inlet))
                continue;
            if (row.Ticks * (int)MetresPerSecond < (int)MinSpacingMetres * TickClock.TickHz)
                return true;
        }

        return false;
    }

    private void AdvanceTicks(int ticks)
    {
        if (ticks == 0) return;
        for (int i = _moving.Count - 1; i >= 0; i--)
        {
            var row = _moving[i];
            row.Ticks += ticks;
            float metres = row.Ticks * MetresPerSecond / TickClock.TickHz;
            var updated = row.Capsule with { MetresAlongPath = metres };
            if (row.Ticks * (int)MetresPerSecond >= row.Hops * (int)TileMetres * TickClock.TickHz)
            {
                if (TryEmitOrReroute(row, updated))
                    _moving.RemoveAt(i);
                else
                    row.Capsule = updated;
                continue;
            }

            row.Capsule = updated;
        }
    }

    private bool TryEmitOrReroute(InFlight row, Capsule updated)
    {
        if (!_blocked.Contains(updated.TargetOutlet))
        {
            _emitted[updated.TargetOutlet].Add(updated);
            return true;
        }

        var item = new BeltItem(updated.ItemId, 0f, updated.Kind, updated.Address);
        if (!TryRoute(row.Inlet, item, out var alt) || !TryPathHops(row.Inlet, alt, out int hops))
            return false;

        row.Capsule = updated with { TargetOutlet = alt };
        row.Hops = hops;
        if (row.Ticks * (int)MetresPerSecond < hops * (int)TileMetres * TickClock.TickHz)
            return false;

        _emitted[alt].Add(row.Capsule);
        return true;
    }

    private bool TryPathHops(TileCoord start, TileCoord goal, out int hops)
    {
        hops = 0;
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
                var walk = at;
                while (!walk.Equals(start))
                {
                    hops++;
                    walk = parent[walk];
                }

                return true;
            }

            for (int d = 0; d < 4; d++)
            {
                var next = BeltNetwork.Next(at, (Facing)d);
                if (_blocked.Contains(next) || !Walkable(next) || !visited.Add(next))
                    continue;
                parent[next] = at;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private bool Walkable(TileCoord tile) => _nodes.Contains(tile) || _climbs.Contains(tile);

    private static bool IsFamily(string defId) =>
        string.Equals(defId, PipeId, StringComparison.Ordinal)
        || string.Equals(defId, InletId, StringComparison.Ordinal)
        || string.Equals(defId, OutletId, StringComparison.Ordinal)
        || string.Equals(defId, JunctionId, StringComparison.Ordinal)
        || string.Equals(defId, UndergroundId, StringComparison.Ordinal);

    private static int CompareTiles(TileCoord a, TileCoord b)
    {
        int byX = a.X.CompareTo(b.X);
        return byX != 0 ? byX : a.Y.CompareTo(b.Y);
    }

    private readonly record struct MappedFilter(AddressFilter Filter, TileCoord Outlet);

    private sealed class InFlight
    {
        public InFlight(Capsule capsule, TileCoord inlet, int hops)
        {
            Capsule = capsule;
            Inlet = inlet;
            Hops = hops;
        }

        public Capsule Capsule;
        public TileCoord Inlet;
        public int Hops;
        public int Ticks;
    }
}
