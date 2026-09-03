using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Client.UI;

public static class OverlayBootReplica
{
    public static readonly AddressId Larch13 = new(1, 1, 13, 0);

    public static OverlayReplica Build()
    {
        var catalog = LetterCatalog.Instance;
        var auth = new InventorySystem(catalog);
        var player = new EntityId(1);
        var hotbarId = auth.CreateContainer(ContainerSpec.Hotbar, player);
        var inventoryId = auth.CreateContainer(ContainerSpec.BaseInventory, player);
        var backpackId = auth.CreateContainer(ContainerSpec.Backpack, player);
        var chestId = auth.CreateContainer(ContainerSpec.Chest);
        var mail = MailStack.Single(MailKinds.Letter, Larch13, new MailId(1));
        if (auth.Apply(Actor.System, new Deposit(hotbarId, mail)) is not Accepted)
            throw new InvalidOperationException("boot replica could not deposit the hotbar MailStack.");

        EntryId pending = default;
        foreach (var entry in auth[hotbarId].Entries)
            pending = entry.Id;
        if (pending.IsNone)
            throw new InvalidOperationException("boot replica hotbar has no entry to tag pending.");

        var replica = new InventorySystem(catalog);
        Apply(replica, auth.Snapshot(hotbarId));
        Apply(replica, auth.Snapshot(inventoryId));
        Apply(replica, auth.Snapshot(backpackId));
        Apply(replica, auth.Snapshot(chestId));

        return new OverlayReplica(
            replica[hotbarId],
            replica[inventoryId],
            replica[backpackId],
            replica[chestId],
            new HashSet<EntryId> { pending });
    }

    private static void Apply(InventorySystem replica, ContainerDelta delta)
    {
        if (replica.ApplyDelta(delta) != ReplicaResult.Applied)
            throw new InvalidOperationException("boot replica ApplyDelta failed.");
    }

    private sealed class LetterCatalog : IStackCatalog
    {
        public static readonly LetterCatalog Instance = new();

        public Footprint FootprintOf(StackKey key) => new(1, 1);

        public int MaxStackOf(StackKey key) => 20;

        public WeightClass WeightOf(StackKey key) => WeightClass.Light;

        public StackCategory CategoryOf(StackKey key) => StackCategory.Mail;
    }
}
