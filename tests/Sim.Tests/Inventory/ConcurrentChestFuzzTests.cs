using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Tests.Inventory;

public sealed class ConcurrentChestFuzzTests
{
    [Fact]
    public void TwoPlayers_SharedChest_10kRandomizedOps_ConservesMailIds()
    {
        const int seed = 7;
        const int ops = 10_000;
        var rng = new Random(seed);
        var world = new InventoryHarness();
        InventorySeed.Letters(world, world.Chest, addresses: 6, count: 40);
        InventorySeed.Packages(world, world.InvA, world.InvB);

        var ledger = InventoryAudit.Of(world.Inv);
        var replica = new InventorySystem(world.Catalog);
        foreach (var container in world.Inv.Containers)
            Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(world.Inv.Snapshot(container.Id)));

        var stale = world.Inv.Fork();
        for (int i = 0; i < ops; i++)
        {
            if (i % 5 == 0)
                stale = world.Inv.Fork();

            var actor = rng.Next(2) == 0 ? world.PlayerA : world.PlayerB;
            var mine = actor.Equals(world.PlayerA) ? world.InvA : world.InvB;
            var op = OpGen.Random(rng, stale, actor, mine, world.Chest);
            var result = world.Inv.Apply(Actor.Player(actor), op);

            if (result is Accepted accepted)
            {
                foreach (var delta in accepted.Deltas)
                    Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(delta));
            }

            Assert.Equal(ledger, InventoryAudit.Of(world.Inv));
            Assert.Null(world.Inv.CheckInvariants());
        }

        foreach (var container in world.Inv.Containers)
        {
            Assert.True(replica.TryGetContainer(container.Id, out var copy));
            Assert.Equal(container.Hash, copy.Hash);
            Assert.Equal(container.Version, copy.Version);
        }
    }
}

internal static class InventorySeed
{
    public static void Letters(InventoryHarness world, ContainerId into, int addresses, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var address = new AddressId(1, 1, (byte)((i % addresses) + 1), 0);
            Assert.True(world.Deposit(into, world.Letter(address, 1)));
        }
    }

    public static void Packages(InventoryHarness world, params ContainerId[] into)
    {
        var address = new AddressId(1, 2, 1, 0);
        foreach (var container in into)
            Assert.True(world.Deposit(container, world.Package(address)));
    }
}

internal static class OpGen
{
    public static InventoryOp Random(
        Random rng,
        InventorySystem view,
        EntityId actor,
        ContainerId mine,
        ContainerId shared)
    {
        var choices = new List<(ContainerId From, EntryId Entry, ContainerId To)>();
        Collect(view, mine, shared, choices);
        Collect(view, shared, mine, choices);
        if (choices.Count == 0)
            return new Move(shared, new EntryId(1), mine, Placement.Origin);

        var pick = choices[rng.Next(choices.Count)];
        var amount = rng.Next(4) == 0 ? Amount.Of(1) : Amount.All;
        if (rng.Next(2) == 0)
            return new QuickMove(pick.From, pick.Entry, pick.To, amount);

        var dest = view.TryGetContainer(pick.To, out var grid) ? grid : view[pick.From];
        var at = new Placement(
            (byte)rng.Next(dest.Spec.Shape.Cols),
            (byte)rng.Next(dest.Spec.Shape.Rows),
            rng.Next(2) == 0);
        return new Move(pick.From, pick.Entry, pick.To, at, amount);
    }

    private static void Collect(
        InventorySystem view,
        ContainerId from,
        ContainerId to,
        List<(ContainerId From, EntryId Entry, ContainerId To)> into)
    {
        if (!view.TryGetContainer(from, out var grid)) return;
        foreach (var entry in grid.Entries)
            into.Add((from, entry.Id, to));
    }
}
