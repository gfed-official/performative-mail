using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Inventory;

public enum SortKey : byte { ByAddress, BySize }

public sealed class GridContainer
{
    private readonly IStackCatalog _catalog;
    private readonly Dictionary<EntryId, Entry> _entries = new();
    private readonly uint[] _cells;

    public ContainerId Id { get; }

    public ContainerSpec Spec { get; private set; }

    public ContainerVersion Version { get; private set; }

    public ulong Hash { get; private set; }

    public IReadOnlyCollection<Entry> Entries => _entries.Values;

    internal GridContainer(ContainerId id, ContainerSpec spec, IStackCatalog catalog)
    {
        Id = id;
        Spec = spec;
        _catalog = catalog;
        _cells = new uint[spec.Shape.CellCount];
    }

    public bool TryGetEntry(EntryId id, out Entry entry) => _entries.TryGetValue(id, out entry);

    public EntryId EntryAt(Cell cell) => new(_cells[Index(cell)]);

    public Footprint FootprintOf(in Entry entry)
    {
        var fp = _catalog.FootprintOf(entry.Stack.Key);
        return entry.At.Rotated ? fp.Rotated : fp;
    }

    public CellRect RectOf(in Entry entry)
    {
        if (!TryRect(entry.At, entry.Stack.Key, out var rect))
            throw new InvalidOperationException("Entry placement does not fit this container.");
        return rect;
    }

    public int WeightPoints
    {
        get
        {
            int sum = 0;
            foreach (var entry in _entries.Values)
                sum += entry.Stack.Count * (int)_catalog.WeightOf(entry.Stack.Key);
            return sum;
        }
    }

    public string? CheckInvariants()
    {
        var rebuilt = new uint[_cells.Length];
        ulong hash = 0;
        foreach (var entry in _entries.Values)
        {
            if (entry.Id.IsNone) return "none entry id";
            if (entry.Stack.Count < 1) return "empty stack";
            if (entry.Stack is MailStack mail && mail.Ids.Count != entry.Stack.Count)
                return "mail id count";
            var fp = _catalog.FootprintOf(entry.Stack.Key);
            if (!Placement.For(fp, entry.At.X, entry.At.Y, entry.At.Rotated).Equals(entry.At))
                return "placement not normalized";
            if (!Spec.Shape.TryRect(entry.At, fp, out var rect))
                return "entry out of bounds";
            foreach (var cell in rect.Cells())
            {
                int i = Index(cell);
                if (rebuilt[i] != 0) return "overlap";
                rebuilt[i] = entry.Id.Value;
            }

            hash ^= HashEntry(entry);
        }

        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] != rebuilt[i]) return "occupancy drift";
            if (_cells[i] != 0 && !_entries.ContainsKey(new EntryId(_cells[i])))
                return "orphan occupancy";
        }

        if (hash != Hash) return "hash drift";
        return null;
    }

    internal bool TryRect(Placement at, StackKey key, out CellRect rect)
        => Spec.Shape.TryRect(at, _catalog.FootprintOf(key), out rect);

    internal IReadOnlyList<EntryId> Blockers(CellRect rect, EntryId ignore)
    {
        List<EntryId>? ids = null;
        foreach (var cell in rect.Cells())
        {
            var id = EntryAt(cell);
            if (id.IsNone || id.Equals(ignore)) continue;
            if (ids != null && ids.Contains(id)) continue;
            (ids ??= new List<EntryId>()).Add(id);
        }

        if (ids is null) return Array.Empty<EntryId>();
        return ids;
    }

    internal IReadOnlyList<Change> PlanFit(Stack incoming, bool allowPartial, Func<EntryId> allocate, out Stack? leftover)
    {
        Stack? remaining = incoming;
        var changes = new List<Change>();
        var seen = new HashSet<uint>();
        for (int i = 0; i < _cells.Length && remaining != null; i++)
        {
            uint idVal = _cells[i];
            if (idVal == 0 || !seen.Add(idVal)) continue;
            var existing = _entries[new EntryId(idVal)];
            if (!existing.Stack.Key.Equals(remaining.Key)) continue;
            int room = _catalog.MaxStackOf(remaining.Key) - existing.Stack.Count;
            if (room <= 0) continue;
            int k = Math.Min(room, remaining.Count);
            var taken = remaining.Take(k, out remaining);
            changes.Add(new Upsert(new Entry(existing.Id, existing.Stack.Merge(taken), existing.At)));
        }

        if (remaining != null)
            TryPlaceRemaining(ref remaining, changes, allocate);

        if (remaining != null && !allowPartial)
        {
            leftover = incoming;
            return Array.Empty<Change>();
        }

        leftover = remaining;
        return changes;
    }

    internal bool TryRepack(SortKey key, out IReadOnlyList<Entry> packed)
        => throw new NotImplementedException();

    internal bool Apply(Change change)
    {
        switch (change)
        {
            case Upsert u:
                return ApplyUpsert(u.Entry);
            case Remove r:
                return ApplyRemove(r.Id);
            case Reset reset:
                return ApplyReset(reset);
            default:
                throw new NotSupportedException(change.GetType().Name);
        }
    }

    internal void Bump() => Version = Version.Next;

    internal void SetVersion(ContainerVersion version) => Version = version;

    internal GridContainer Clone() => throw new NotImplementedException();

    private void TryPlaceRemaining(ref Stack? remaining, List<Change> changes, Func<EntryId> allocate)
    {
        if (remaining is null) return;
        Stack current = remaining;
        var fp = _catalog.FootprintOf(current.Key);
        if (TryFirstFit(current, rotated: false, changes, allocate, ref remaining))
            return;
        if (fp.IsSquare || remaining is null) return;
        TryFirstFit(remaining, rotated: true, changes, allocate, ref remaining);
    }

    private bool TryFirstFit(
        Stack remaining,
        bool rotated,
        List<Change> changes,
        Func<EntryId> allocate,
        ref Stack? leftover)
    {
        var fp = _catalog.FootprintOf(remaining.Key);
        int max = _catalog.MaxStackOf(remaining.Key);
        for (byte y = 0; y < Spec.Shape.Rows; y++)
        for (byte x = 0; x < Spec.Shape.Cols; x++)
        {
            var at = Placement.For(fp, x, y, rotated);
            if (!TryRect(at, remaining.Key, out var rect)) continue;
            if (Blockers(rect, EntryId.None).Count != 0) continue;
            int cap = Math.Min(remaining.Count, max);
            var placed = remaining.Take(cap, out leftover);
            changes.Add(new Upsert(new Entry(allocate(), placed, at)));
            return true;
        }

        leftover = remaining;
        return false;
    }

    private bool ApplyUpsert(Entry entry)
    {
        if (entry.Id.IsNone) return false;
        if (!TryRect(entry.At, entry.Stack.Key, out var rect)) return false;
        foreach (var cell in rect.Cells())
        {
            uint occupant = _cells[Index(cell)];
            if (occupant != 0 && occupant != entry.Id.Value) return false;
        }

        if (_entries.TryGetValue(entry.Id, out var old))
        {
            Hash ^= HashEntry(old);
            Vacate(old);
        }

        Occupy(rect, entry.Id);
        _entries[entry.Id] = entry;
        Hash ^= HashEntry(entry);
        return true;
    }

    private bool ApplyRemove(EntryId id)
    {
        if (!_entries.TryGetValue(id, out var old)) return false;
        Hash ^= HashEntry(old);
        Vacate(old);
        _entries.Remove(id);
        return true;
    }

    private bool ApplyReset(Reset reset)
    {
        if (reset.Spec.Shape.CellCount != _cells.Length) return false;
        Array.Clear(_cells, 0, _cells.Length);
        _entries.Clear();
        Hash = 0;
        Spec = reset.Spec;
        foreach (var entry in reset.Entries)
            if (!ApplyUpsert(entry)) return false;
        return true;
    }

    private void Vacate(in Entry entry)
    {
        if (!TryRect(entry.At, entry.Stack.Key, out var rect)) return;
        foreach (var cell in rect.Cells())
            _cells[Index(cell)] = 0;
    }

    private void Occupy(CellRect rect, EntryId id)
    {
        foreach (var cell in rect.Cells())
            _cells[Index(cell)] = id.Value;
    }

    private int Index(Cell cell) => cell.Y * Spec.Shape.Cols + cell.X;

    private static ulong HashEntry(in Entry entry)
    {
        ulong h = SplitMix64(entry.Id.Value);
        var key = entry.Stack.Key;
        h ^= SplitMix64((key.IsMail ? 2UL : 1UL) << 32 | key.Def);
        h ^= SplitMix64(key.Address);
        h ^= SplitMix64((ulong)(uint)entry.Stack.Count);
        if (entry.Stack is MailStack mail)
        {
            for (int i = 0; i < mail.Ids.Count; i++)
                h ^= SplitMix64(((ulong)(uint)i << 32) | mail.Ids[i].Value);
        }

        h ^= SplitMix64(
            ((ulong)entry.At.X << 16) |
            ((ulong)entry.At.Y << 8) |
            (entry.At.Rotated ? 1UL : 0UL));
        return h;
    }

    private static ulong SplitMix64(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }
}
