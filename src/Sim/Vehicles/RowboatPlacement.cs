using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Vehicles;

public enum VehiclePlaceReject : byte
{
    MissingBlueprint,
    UnknownItem,
    MissingInput
}

public abstract record VehiclePlaceResult;

public sealed record VehiclePlaced(VehicleBody Vehicle) : VehiclePlaceResult;

public sealed record VehiclePlaceRejected(VehiclePlaceReject Reason) : VehiclePlaceResult;

public static class RowboatPlacement
{
    public const string ItemId = "rowboat";
    public const string BlueprintId = SeaKit.BlueprintId;
    public const string LogItem = "log";
    public const string RopeItem = "rope";
    public const int LogCount = 8;
    public const int RopeCount = 2;

    public static VehiclePlaceResult TryPlace(
        IReadOnlyCollection<string> blueprints,
        InventorySystem inventory,
        ContainerId from,
        IReadOnlyDictionary<string, ItemDefId> itemIds,
        VehicleTable vehicles,
        in PlayerPose pose)
    {
        if (blueprints is null) throw new ArgumentNullException(nameof(blueprints));
        if (inventory is null) throw new ArgumentNullException(nameof(inventory));
        if (itemIds is null) throw new ArgumentNullException(nameof(itemIds));
        if (vehicles is null) throw new ArgumentNullException(nameof(vehicles));

        if (!SeaKit.OwnsRowboatBlueprint(blueprints))
            return new VehiclePlaceRejected(VehiclePlaceReject.MissingBlueprint);
        if (!TryInputs(inventory, from, itemIds, consume: true, out var reject))
            return new VehiclePlaceRejected(reject);

        return new VehiclePlaced(vehicles.SpawnRowboat(in pose));
    }

    public static VehiclePlaceReject? Preview(
        IReadOnlyCollection<string> blueprints,
        InventorySystem inventory,
        ContainerId from,
        IReadOnlyDictionary<string, ItemDefId> itemIds)
    {
        if (blueprints is null) throw new ArgumentNullException(nameof(blueprints));
        if (inventory is null) throw new ArgumentNullException(nameof(inventory));
        if (itemIds is null) throw new ArgumentNullException(nameof(itemIds));

        if (!SeaKit.OwnsRowboatBlueprint(blueprints))
            return VehiclePlaceReject.MissingBlueprint;
        if (!TryInputs(inventory, from, itemIds, consume: false, out var reject))
            return reject;
        return null;
    }

    private static bool TryInputs(
        InventorySystem inventory,
        ContainerId from,
        IReadOnlyDictionary<string, ItemDefId> itemIds,
        bool consume,
        out VehiclePlaceReject reject)
    {
        if (!inventory.TryGetContainer(from, out var grid))
        {
            reject = VehiclePlaceReject.MissingInput;
            return false;
        }

        var need = new[]
        {
            (LogItem, LogCount),
            (RopeItem, RopeCount)
        };
        var takes = new List<(EntryId Id, int Count)>();
        for (int i = 0; i < need.Length; i++)
        {
            var (item, count) = need[i];
            if (!itemIds.TryGetValue(item, out var itemId))
            {
                reject = VehiclePlaceReject.UnknownItem;
                return false;
            }

            int remaining = count;
            foreach (var entry in grid.Entries)
            {
                if (remaining < 1) break;
                if (entry.Stack is not ItemStack stack || !stack.Item.Equals(itemId))
                    continue;
                int take = stack.Count < remaining ? stack.Count : remaining;
                takes.Add((entry.Id, take));
                remaining -= take;
            }

            if (remaining > 0)
            {
                reject = VehiclePlaceReject.MissingInput;
                return false;
            }
        }

        if (!consume)
        {
            reject = default;
            return true;
        }

        for (int i = 0; i < takes.Count; i++)
        {
            var step = takes[i];
            if (inventory.Apply(Actor.System, new Withdraw(from, step.Id, Amount.Of(step.Count))) is not Accepted)
            {
                reject = VehiclePlaceReject.MissingInput;
                return false;
            }
        }

        reject = default;
        return true;
    }
}
