using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Inventory;

public enum RejectReason : byte
{
    UnknownContainer,
    UnknownEntry,
    NotOpen,
    Forbidden,
    WrongCategory,
    OutOfBounds,
    Occupied,
    StackFull,
    BadCount,
    NoRoom,
    CannotRepack,
}

public readonly struct Actor
{
    public EntityId? PlayerId { get; }

    public bool IsSystem => PlayerId is null;

    private Actor(EntityId? playerId) => PlayerId = playerId;

    public static Actor Player(EntityId id) => new(id);

    public static readonly Actor System = new(null);
}

public abstract record InventoryOp
{
    internal abstract IEnumerable<ContainerId> Touched { get; }

    internal abstract bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason);

    internal static Stack? Combine(Stack? a, Stack? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a.Merge(b);
    }

    internal static bool TryResolveSource(
        InventorySystem inv,
        ContainerId from,
        EntryId entry,
        Amount amount,
        out GridContainer src,
        out Entry sourceEntry,
        out Stack moving,
        out Stack? rest,
        out RejectReason reason)
    {
        src = null!;
        sourceEntry = default;
        moving = null!;
        rest = null;
        if (!inv.TryGetContainer(from, out src))
        {
            reason = RejectReason.UnknownContainer;
            return false;
        }

        if (!src.TryGetEntry(entry, out sourceEntry))
        {
            reason = RejectReason.UnknownEntry;
            return false;
        }

        int n = amount.Resolve(sourceEntry.Stack.Count);
        if (n < 1 || n > sourceEntry.Stack.Count)
        {
            reason = RejectReason.BadCount;
            return false;
        }

        moving = sourceEntry.Stack.Take(n, out rest);
        reason = default;
        return true;
    }
}

public sealed record Move(
    ContainerId From,
    EntryId Entry,
    ContainerId To,
    Placement At,
    Amount Count = default) : InventoryOp
{
    internal override IEnumerable<ContainerId> Touched =>
        From.Equals(To) ? new[] { From } : new[] { From, To };

    internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
    {
        plan = Plan.Empty;
        if (!TryResolveSource(inv, From, Entry, Count, out var src, out var sourceEntry, out var moving, out var rest, out reason))
            return false;

        if (!inv.TryGetContainer(To, out var dst))
        {
            reason = RejectReason.UnknownContainer;
            return false;
        }

        if (!dst.Spec.Accepts(inv.Catalog.CategoryOf(moving.Key)))
        {
            reason = RejectReason.WrongCategory;
            return false;
        }

        var fp = inv.Catalog.FootprintOf(moving.Key);
        var at = dst.Spec.Shape.IgnoresFootprint
            ? Placement.Origin
            : Placement.For(fp, At.X, At.Y, At.Rotated);

        if (!dst.TryRect(at, moving.Key, out var rect))
        {
            reason = RejectReason.OutOfBounds;
            return false;
        }

        var ignore = From.Equals(To) && rest is null ? sourceEntry.Id : EntryId.None;
        var blockers = dst.Blockers(rect, ignore);
        var steps = new List<(ContainerId, Change)>();

        if (blockers.Count == 0)
        {
            if (rest is null && From.Equals(To) && src.RectOf(sourceEntry).Equals(rect))
            {
                reason = default;
                plan = Plan.Empty;
                return true;
            }

            if (rest is null)
            {
                if (!From.Equals(To))
                    steps.Add((From, new Remove(sourceEntry.Id)));
                steps.Add((To, new Upsert(new Entry(sourceEntry.Id, moving, at))));
            }
            else
            {
                steps.Add((From, new Upsert(new Entry(sourceEntry.Id, rest, sourceEntry.At))));
                steps.Add((To, new Upsert(new Entry(inv.AllocateEntryId(), moving, at))));
            }

            plan = new Plan(steps);
            reason = default;
            return true;
        }

        if (blockers.Count == 1)
        {
            var targetId = blockers[0];
            if (!dst.TryGetEntry(targetId, out var target) || !target.Stack.Key.Equals(moving.Key) || targetId.Equals(sourceEntry.Id))
            {
                reason = RejectReason.Occupied;
                return false;
            }

            int room = inv.Catalog.MaxStackOf(moving.Key) - target.Stack.Count;
            if (room <= 0)
            {
                reason = RejectReason.StackFull;
                return false;
            }

            int k = Math.Min(room, moving.Count);
            var taken = moving.Take(k, out var excess);
            var merged = target.Stack.Merge(taken);
            var leftover = Combine(rest, excess);
            steps.Add((To, new Upsert(new Entry(target.Id, merged, target.At))));
            steps.Add((From, leftover is null
                ? new Remove(sourceEntry.Id)
                : new Upsert(new Entry(sourceEntry.Id, leftover, sourceEntry.At))));
            plan = new Plan(steps);
            reason = default;
            return true;
        }

        reason = RejectReason.Occupied;
        return false;
    }
}

public sealed record QuickMove(
    ContainerId From,
    EntryId Entry,
    ContainerId To,
    Amount Count = default) : InventoryOp
{
    internal override IEnumerable<ContainerId> Touched =>
        From.Equals(To) ? new[] { From } : new[] { From, To };

    internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
    {
        plan = Plan.Empty;
        if (From.Equals(To))
        {
            reason = default;
            return true;
        }

        if (!TryResolveSource(inv, From, Entry, Count, out _, out var sourceEntry, out var moving, out var rest, out reason))
            return false;

        if (!inv.TryGetContainer(To, out var dst))
        {
            reason = RejectReason.UnknownContainer;
            return false;
        }

        if (!dst.Spec.Accepts(inv.Catalog.CategoryOf(moving.Key)))
        {
            reason = RejectReason.WrongCategory;
            return false;
        }

        var fit = dst.PlanFit(moving, allowPartial: true, inv.AllocateEntryId, out var leftover);
        if (fit.Count == 0)
        {
            reason = RejectReason.NoRoom;
            return false;
        }

        var steps = new List<(ContainerId, Change)>();
        foreach (var change in fit)
            steps.Add((To, change));

        var remaining = Combine(rest, leftover);
        steps.Add((From, remaining is null
            ? new Remove(sourceEntry.Id)
            : new Upsert(new Entry(sourceEntry.Id, remaining, sourceEntry.At))));

        plan = new Plan(steps);
        reason = default;
        return true;
    }
}

public sealed record Sort(ContainerId Container, SortKey Key) : InventoryOp
{
    internal override IEnumerable<ContainerId> Touched => new[] { Container };

    internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
    {
        plan = Plan.Empty;
        if (!inv.TryGetContainer(Container, out var container))
        {
            reason = RejectReason.UnknownContainer;
            return false;
        }

        if (!container.TryRepack(Key, out var packed))
        {
            reason = RejectReason.CannotRepack;
            return false;
        }

        plan = new Plan(new[] { (Container, (Change)new Reset(container.Spec, packed)) });
        reason = default;
        return true;
    }
}

public sealed record Withdraw(ContainerId From, EntryId Entry, Amount Count = default) : InventoryOp
{
    internal override IEnumerable<ContainerId> Touched => new[] { From };

    internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
    {
        plan = Plan.Empty;
        if (!TryResolveSource(inv, From, Entry, Count, out _, out var sourceEntry, out var taken, out var rest, out reason))
            return false;

        var change = rest is null
            ? (Change)new Remove(sourceEntry.Id)
            : new Upsert(new Entry(sourceEntry.Id, rest, sourceEntry.At));
        plan = new Plan(new[] { (From, change) }, taken);
        reason = default;
        return true;
    }
}

public sealed record Deposit(ContainerId To, Stack Stack) : InventoryOp
{
    internal override IEnumerable<ContainerId> Touched => new[] { To };

    internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
    {
        plan = Plan.Empty;
        if (!inv.TryGetContainer(To, out var dst))
        {
            reason = RejectReason.UnknownContainer;
            return false;
        }

        if (!dst.Spec.Accepts(inv.Catalog.CategoryOf(Stack.Key)))
        {
            reason = RejectReason.WrongCategory;
            return false;
        }

        var fit = dst.PlanFit(Stack, allowPartial: false, inv.AllocateEntryId, out var leftover);
        if (fit.Count == 0 || leftover != null)
        {
            reason = RejectReason.NoRoom;
            return false;
        }

        var steps = new List<(ContainerId, Change)>(fit.Count);
        foreach (var change in fit)
            steps.Add((To, change));
        plan = new Plan(steps);
        reason = default;
        return true;
    }
}

public sealed class Plan
{
    internal IReadOnlyList<(ContainerId Container, Change Change)> Steps { get; }

    internal Stack? Withdrawn { get; }

    internal Plan(IReadOnlyList<(ContainerId Container, Change Change)> steps, Stack? withdrawn = null)
    {
        Steps = steps;
        Withdrawn = withdrawn;
    }

    internal static readonly Plan Empty = new(Array.Empty<(ContainerId, Change)>());
}

public abstract record InventoryOpResult;

public sealed record Accepted(IReadOnlyList<ContainerDelta> Deltas, Stack? Withdrawn = null) : InventoryOpResult;

public sealed record Rejected(RejectReason Reason) : InventoryOpResult;

public enum ReplicaResult : byte
{
    Applied,
    VersionGap,
    Conflict,
    HashMismatch,
}
