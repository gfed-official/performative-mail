using PerformativeMail.BotClient;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class JoinStateSizeTests
{
    private const uint FixedSeed = 0x7F3A9C21;
    private const ulong GoldenWorldHash = 0x821670054873680EUL;

    [Fact]
    public void PrepShift3_TypicalContainers_EncodedJoinStateAtMost200KB()
    {
        var world = TypicalShift3World();
        var catalog = world.Inventory!.Catalog;
        var hub = LoopbackHub.ForSeats(3);
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(hub.ServerEnds),
            world,
            new WorldOffer(FixedSeed, GoldenWorldHash),
            offeredSettings: null,
            new RunState(RunPhase.Prep, 3, 0));

        var clients = new ClientRuntime[3];
        for (int i = 0; i < clients.Length; i++)
        {
            clients[i] = new ClientRuntime(catalog);
            clients[i].Connect(hub.ClientEnds[i]);
        }

        server.TickOnce();
        for (int i = 0; i < clients.Length; i++)
            clients[i].Receive();

        var joiner = clients[2];
        Assert.Null(joiner.LastReject);
        Assert.NotNull(joiner.AcceptedJoin);
        Assert.Equal(3, joiner.AcceptedJoin.Value.Run.Shift);
        Assert.Equal(RunPhase.Prep, joiner.AcceptedJoin.Value.Run.Phase);

        byte[] encoded = WireCodec.Encode(joiner.AcceptedJoin.Value);
        Console.WriteLine($"join-state {encoded.Length}B");
        Assert.True(encoded.Length <= JoinState.MaxEncodedBytes);

        Assert.Equal(GoldenWorldHash, joiner.AcceptedWorldHash);
        Assert.NotNull(joiner.GeneratedWorld);
        Assert.Equal(GoldenWorldHash, WorldHash.Compute(joiner.GeneratedWorld));
        AssertContainerHashesMatch(world.Inventory, joiner.AcceptedJoin.Value);
    }

    private static SimWorld TypicalShift3World()
    {
        var world = BotWorld.CreateShift1World();
        BotWorld.DepositShift1Letter(world);
        var chest = world.Inventory!.CreateContainer(ContainerSpec.Chest);
        var address = world.Atlas!.DeliverableAddresses[0];
        var mailId = world.Mail!.Allocate();
        var item = new MailItem(
            mailId,
            MailKinds.Letter,
            address,
            MailKinds.ValueAtSpawn(MailKinds.Letter, world.Atlas.DistrictId, MailSpawnConstants.Shift1),
            MailSpawnConstants.Shift1,
            MailSpawnConstants.Shift1);
        Assert.True(world.Mail.Register(item));
        Assert.IsType<Accepted>(world.Inventory.Apply(
            Actor.System,
            new Deposit(chest, MailStack.Single(item.Kind, item.Address, item.Id))));
        return world;
    }

    private static void AssertContainerHashesMatch(InventorySystem inventory, JoinState join)
    {
        int hostCount = 0;
        foreach (var container in inventory.Containers)
            hostCount++;

        Assert.True(hostCount >= 2);
        Assert.Equal(hostCount, join.Containers.Count);
        for (int i = 0; i < join.Containers.Count; i++)
        {
            var stamp = join.Containers[i];
            Assert.True(inventory.TryGetContainer(stamp.Id, out var grid));
            Assert.Equal(grid.Version, stamp.Version);
            Assert.Equal(grid.Hash, stamp.Hash);
        }
    }
}
