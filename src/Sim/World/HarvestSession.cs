using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.World;

public enum HarvestReject : byte
{
    UnknownNode,
    WrongTool,
    Exhausted,
    UnknownItem,
    NoRoom
}

public abstract record HarvestResult;

public sealed record Harvested(
    ResourceKind Kind,
    string Item,
    int Count,
    int HitsLeft,
    HarvestRemnant Remnant) : HarvestResult;

public sealed record HarvestRejected(HarvestReject Reason) : HarvestResult;

public readonly record struct HarvestNodeState(ResourceKind Kind, int HitsLeft)
{
    public HarvestRemnant Remnant =>
        HitsLeft > 0 ? HarvestRemnant.Live : HarvestTable.Of(Kind).After;
}

public sealed class HarvestSession
{
    private readonly Dictionary<TileCoord, HarvestNodeState> _nodes = new();
    private readonly InventorySystem? _inventory;
    private readonly ContainerId _grantTo;
    private readonly Dictionary<string, ItemDefId> _itemIds;

    public HarvestSession(
        IReadOnlyList<ResourceNodeRecord> nodes,
        InventorySystem? inventory = null,
        ContainerId grantTo = default,
        IReadOnlyDictionary<string, ItemDefId>? itemIds = null)
    {
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));

        for (int i = 0; i < nodes.Count; i++)
        {
            var placed = nodes[i];
            var spec = HarvestTable.Of(placed.Kind);
            if (_nodes.ContainsKey(placed.Tile))
                throw new ArgumentException("Each resource node must occupy a unique tile.", nameof(nodes));
            _nodes.Add(placed.Tile, new HarvestNodeState(placed.Kind, spec.Hits));
        }

        _inventory = inventory;
        _grantTo = grantTo;
        _itemIds = new Dictionary<string, ItemDefId>(StringComparer.Ordinal);
        if (itemIds is null) return;
        foreach (var pair in itemIds)
            _itemIds[pair.Key] = pair.Value;
    }

    public bool TryGet(TileCoord tile, out HarvestNodeState state)
        => _nodes.TryGetValue(tile, out state);

    public HarvestResult Hit(TileCoord tile, HarvestTool tool)
    {
        if (!_nodes.TryGetValue(tile, out var state))
            return new HarvestRejected(HarvestReject.UnknownNode);
        if (state.HitsLeft < 1)
            return new HarvestRejected(HarvestReject.Exhausted);

        var spec = HarvestTable.Of(state.Kind);
        if (!spec.Allows(tool))
            return new HarvestRejected(HarvestReject.WrongTool);

        int count = HarvestTable.YieldFor(state.Kind, tool);
        if (_inventory is not null)
        {
            if (!_itemIds.TryGetValue(spec.ItemId, out var itemId))
                return new HarvestRejected(HarvestReject.UnknownItem);
            var deposited = _inventory.Apply(Actor.System, new Deposit(_grantTo, new ItemStack(itemId, count)));
            if (deposited is not Accepted)
                return new HarvestRejected(HarvestReject.NoRoom);
        }

        var next = new HarvestNodeState(state.Kind, state.HitsLeft - 1);
        _nodes[tile] = next;
        return new Harvested(state.Kind, spec.ItemId, count, next.HitsLeft, next.Remnant);
    }
}
