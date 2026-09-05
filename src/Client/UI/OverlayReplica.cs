using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Client.UI;

public readonly record struct OverlayReplica(
    GridContainer Hotbar,
    GridContainer Inventory,
    GridContainer? Backpack,
    GridContainer? External,
    IReadOnlySet<EntryId> Pending)
{
    public static readonly IReadOnlySet<EntryId> NoPending = new HashSet<EntryId>();

    public OverlayStamp Stamp() => OverlayStamp.From(in this);
}

public readonly record struct OverlayStamp(
    ContainerId HotbarId,
    ContainerVersion HotbarVersion,
    ulong HotbarHash,
    ContainerId InventoryId,
    ContainerVersion InventoryVersion,
    ulong InventoryHash,
    ContainerId BackpackId,
    ContainerVersion BackpackVersion,
    ulong BackpackHash,
    bool HasBackpack,
    ContainerId ExternalId,
    ContainerVersion ExternalVersion,
    ulong ExternalHash,
    bool HasExternal,
    int PendingCount,
    ulong PendingMix)
{
    public static OverlayStamp From(in OverlayReplica replica)
    {
        GridStamp(replica.Backpack, out var backpackId, out var backpackVersion, out var backpackHash);
        GridStamp(replica.External, out var externalId, out var externalVersion, out var externalHash);
        return new OverlayStamp(
            replica.Hotbar.Id,
            replica.Hotbar.Version,
            replica.Hotbar.Hash,
            replica.Inventory.Id,
            replica.Inventory.Version,
            replica.Inventory.Hash,
            backpackId,
            backpackVersion,
            backpackHash,
            replica.Backpack is not null,
            externalId,
            externalVersion,
            externalHash,
            replica.External is not null,
            replica.Pending.Count,
            MixPending(replica.Pending));
    }

    private static void GridStamp(
        GridContainer? grid,
        out ContainerId id,
        out ContainerVersion version,
        out ulong hash)
    {
        if (grid is null)
        {
            id = default;
            version = default;
            hash = 0;
            return;
        }

        id = grid.Id;
        version = grid.Version;
        hash = grid.Hash;
    }

    private static ulong MixPending(IReadOnlySet<EntryId> pending)
    {
        ulong mix = 0;
        foreach (var id in pending)
            mix ^= id.Value;
        return mix;
    }
}
