using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Inventory;

public sealed class InventorySystem
{
    private readonly IStackCatalog _catalog;
    private readonly Dictionary<ContainerId, GridContainer> _containers = new();
    private readonly Dictionary<ContainerId, EntityId> _owners = new();
    private readonly Dictionary<EntityId, ContainerId> _external = new();
    private readonly List<ContainerDelta> _committed = new();
    private uint _nextContainer = 1;
    private uint _nextEntry = 1;

    public InventorySystem(IStackCatalog catalog) => _catalog = catalog;

    private InventorySystem(InventorySystem source)
    {
        _catalog = source._catalog;
        _nextContainer = source._nextContainer;
        _nextEntry = source._nextEntry;
        foreach (var pair in source._containers)
            _containers[pair.Key] = pair.Value.Clone();
        foreach (var pair in source._owners)
            _owners[pair.Key] = pair.Value;
        foreach (var pair in source._external)
            _external[pair.Key] = pair.Value;
    }

    public IStackCatalog Catalog => _catalog;

    public IEnumerable<GridContainer> Containers => _containers.Values;

    public GridContainer this[ContainerId id] => _containers[id];

    public bool TryGetContainer(ContainerId id, out GridContainer container)
        => _containers.TryGetValue(id, out container);

    public ContainerId CreateContainer(ContainerSpec spec, EntityId? owner = null)
    {
        var id = new ContainerId(_nextContainer++);
        _containers[id] = new GridContainer(id, spec, _catalog);
        if (owner is { } o)
            _owners[id] = o;
        return id;
    }

    public IReadOnlyList<Stack> DestroyContainer(ContainerId id)
    {
        var grid = _containers[id];
        var stacks = new List<Stack>(grid.Entries.Count);
        foreach (var entry in grid.Entries)
            stacks.Add(entry.Stack);

        var closed = new List<EntityId>();
        foreach (var pair in _external)
        {
            if (pair.Value.Equals(id))
                closed.Add(pair.Key);
        }

        foreach (var player in closed)
            _external.Remove(player);

        _owners.Remove(id);
        _containers.Remove(id);
        return stacks;
    }

    public InventoryOpResult Apply(Actor actor, InventoryOp op)
    {
        if (!Authorize(actor, op, out var why))
            return new Rejected(why);
        if (!op.TryPlan(this, out var plan, out why))
            return new Rejected(why);
        return Commit(plan);
    }

    public InventoryOpResult Open(EntityId player, ContainerId container)
    {
        if (!_containers.ContainsKey(container))
            return new Rejected(RejectReason.UnknownContainer);

        _external[player] = container;
        var delta = Snapshot(container);
        _committed.Add(delta);
        return new Accepted(new[] { delta });
    }

    public IReadOnlyList<ContainerDelta> DrainCommittedDeltas()
    {
        if (_committed.Count == 0)
            return Array.Empty<ContainerDelta>();

        var drained = _committed.ToArray();
        _committed.Clear();
        return drained;
    }

    public void Close(EntityId player, ContainerId container)
    {
        if (_external.TryGetValue(player, out var open) && open.Equals(container))
            _external.Remove(player);
    }

    public IEnumerable<EntityId> ViewersOf(ContainerId container)
    {
        if (_owners.TryGetValue(container, out var owner))
            yield return owner;

        foreach (var pair in _external)
        {
            if (!pair.Value.Equals(container)) continue;
            if (_owners.TryGetValue(container, out owner) && owner.Equals(pair.Key))
                continue;
            yield return pair.Key;
        }
    }

    public ContainerDelta Snapshot(ContainerId container)
    {
        var grid = _containers[container];
        var entries = new List<Entry>(grid.Entries.Count);
        foreach (var entry in grid.Entries)
            entries.Add(entry);
        return new ContainerDelta(
            container,
            grid.Version,
            grid.Version,
            grid.Hash,
            new[] { (Change)new Reset(grid.Spec, entries) });
    }

    public ReplicaResult ApplyDelta(ContainerDelta delta)
    {
        if (!_containers.TryGetValue(delta.Container, out var grid))
        {
            if (delta.Changes.Count == 0 || delta.Changes[0] is not Reset reset)
                return ReplicaResult.VersionGap;
            grid = new GridContainer(delta.Container, reset.Spec, _catalog);
            _containers[delta.Container] = grid;
            if (delta.Container.Value >= _nextContainer)
                _nextContainer = delta.Container.Value + 1;
        }
        else if (delta.Changes.Count == 0 || delta.Changes[0] is not Reset)
        {
            if (!delta.BeforeVersion.Equals(grid.Version) || !delta.Version.Equals(grid.Version.Next))
                return ReplicaResult.VersionGap;
        }

        foreach (var change in delta.Changes)
        {
            if (!grid.Apply(change))
                return ReplicaResult.Conflict;
            NoteAllocated(change);
        }

        grid.SetVersion(delta.Version);
        if (grid.Hash != delta.Hash)
            return ReplicaResult.HashMismatch;
        return ReplicaResult.Applied;
    }

    public InventorySystem Fork() => new(this);

    public string? CheckInvariants()
    {
        var mail = new HashSet<uint>();
        var entries = new HashSet<uint>();
        foreach (var container in _containers.Values)
        {
            var report = container.CheckInvariants();
            if (report != null)
                return container.Id.Value + ":" + report;
            foreach (var entry in container.Entries)
            {
                if (!entries.Add(entry.Id.Value))
                    return "duplicate entry id";
                if (entry.Stack is not MailStack letters) continue;
                for (int i = 0; i < letters.Ids.Count; i++)
                {
                    if (!mail.Add(letters.Ids[i].Value))
                        return "duplicate mail id";
                }
            }
        }

        return null;
    }

    internal EntryId AllocateEntryId() => new(_nextEntry++);

    internal bool IsOpen(EntityId player, ContainerId container)
        => (_owners.TryGetValue(container, out var owner) && owner.Equals(player))
        || (_external.TryGetValue(player, out var open) && open.Equals(container));

    private bool Authorize(Actor actor, InventoryOp op, out RejectReason reason)
    {
        if (op is Deposit && !actor.IsSystem)
        {
            reason = RejectReason.Forbidden;
            return false;
        }

        if (actor.IsSystem)
        {
            reason = default;
            return true;
        }

        var player = actor.PlayerId!.Value;
        foreach (var containerId in op.Touched)
        {
            if (!_containers.ContainsKey(containerId))
            {
                reason = RejectReason.UnknownContainer;
                return false;
            }

            if (IsOpen(player, containerId)) continue;
            reason = RejectReason.NotOpen;
            return false;
        }

        reason = default;
        return true;
    }

    private Accepted Commit(Plan plan)
    {
        if (plan.Steps.Count == 0)
            return new Accepted(Array.Empty<ContainerDelta>(), plan.Withdrawn);

        var before = InventoryAudit.Of(this);
        var expected = before;
        var order = new List<ContainerId>();
        var grouped = new Dictionary<ContainerId, List<Change>>();
        foreach (var (containerId, change) in plan.Steps)
        {
            if (!grouped.TryGetValue(containerId, out var list))
            {
                list = new List<Change>();
                grouped[containerId] = list;
                order.Add(containerId);
            }

            expected = expected.After(_containers[containerId], change);
            list.Add(change);
        }

        var beforeStamp = new Dictionary<ContainerId, ContainerVersion>();
        foreach (var id in order)
            beforeStamp[id] = _containers[id].Version;

        foreach (var id in order)
        {
            var grid = _containers[id];
            foreach (var change in grouped[id])
            {
                if (grid.Apply(change)) continue;
                throw new InvalidOperationException("Plan produced a change that Apply rejected.");
            }
        }

        var after = InventoryAudit.Of(this);
        if (!after.Equals(expected))
            throw new InvalidOperationException("Inventory conservation failed.");
        if (plan.Withdrawn is { } withdrawn && !after.Plus(withdrawn).Equals(before))
            throw new InvalidOperationException("Withdraw conservation failed.");

        var deltas = new List<ContainerDelta>(order.Count);
        foreach (var id in order)
        {
            var grid = _containers[id];
            grid.Bump();
            deltas.Add(new ContainerDelta(
                id,
                beforeStamp[id],
                grid.Version,
                grid.Hash,
                grouped[id]));
        }

        _committed.AddRange(deltas);
        return new Accepted(deltas, plan.Withdrawn);
    }

    private void NoteAllocated(Change change)
    {
        if (change is Upsert u && u.Entry.Id.Value >= _nextEntry)
            _nextEntry = u.Entry.Id.Value + 1;
        if (change is not Reset reset) return;
        foreach (var entry in reset.Entries)
        {
            if (entry.Id.Value >= _nextEntry)
                _nextEntry = entry.Id.Value + 1;
        }
    }
}
