using System;
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

public sealed class JoinStatePrepTests
{
    private const uint FixedSeed = 0x7F3A9C21;
    private const ulong GoldenWorldHash = 0x821670054873680EUL;

    [Fact]
    public void PrepJoin_ThirdClientHashMatchesHost()
    {
        var hub = LoopbackHub.ForSeats(3);
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(hub.ServerEnds),
            new SimWorld(),
            new WorldOffer(FixedSeed, GoldenWorldHash),
            offeredSettings: null,
            new RunState(RunPhase.Prep, 3, 0));

        var clients = ConnectSeats(hub, 3, catalog: null);
        server.TickOnce();
        ReceiveAll(clients);

        var joiner = clients[2];
        Assert.Null(joiner.LastReject);
        Assert.Equal(GoldenWorldHash, joiner.AcceptedWorldHash);
        Assert.NotNull(joiner.GeneratedWorld);
        Assert.Equal(GoldenWorldHash, WorldHash.Compute(joiner.GeneratedWorld));
        Assert.NotNull(joiner.AcceptedJoin);
    }

    [Fact]
    public void PrepJoin_ThirdClientContainerVersionsMatchHost()
    {
        var catalog = EventCatalog.Instance;
        var world = new SimWorld(catalog);
        var chest = world.Inventory!.CreateContainer(ContainerSpec.Chest);
        var accepted = Assert.IsType<Accepted>(world.Inventory.Apply(
            Actor.System,
            new Deposit(chest, MailStack.Single(MailKinds.Letter, new AddressId(1, 4, 13, 0), new MailId(1)))));
        Assert.NotEmpty(accepted.Deltas);
        Assert.True(world.Inventory.TryGetContainer(chest, out var hostChest));
        Assert.True(hostChest.Version.Value > 0);

        var hub = LoopbackHub.ForSeats(3);
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(hub.ServerEnds),
            world,
            new WorldOffer(FixedSeed, GoldenWorldHash),
            offeredSettings: null,
            new RunState(RunPhase.Prep, 3, 0));

        var clients = ConnectSeats(hub, 3, catalog);
        server.TickOnce();
        ReceiveAll(clients);

        var joiner = clients[2];
        Assert.Null(joiner.LastReject);
        Assert.NotNull(joiner.AcceptedJoin);
        AssertContainerVersionsMatch(world.Inventory, joiner.AcceptedJoin.Value);
    }

    [Fact]
    public void DeliveryHello_RejectsWrongPhase_WithoutSpawn()
    {
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(loopback.A),
            new SimWorld(),
            offeredWorld: null,
            offeredSettings: null,
            new RunState(RunPhase.Delivery, 1, 0));
        var client = new ClientRuntime();
        client.Connect(loopback.B);

        server.TickOnce();
        client.Receive();

        Assert.NotNull(client.LastReject);
        Assert.Equal(HelloRejectReason.WrongPhase, client.LastReject.Value.Reason);
        Assert.Null(client.LocalPlayer);
        Assert.Equal(0, server.JoinedCount);
    }

    [Fact]
    public void LobbyHello_StillOffersSettings()
    {
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A), new SimWorld());
        var client = new ClientRuntime();
        client.Connect(loopback.B);

        server.TickOnce();
        client.Receive();

        Assert.Equal(RunSettings.Arcade(), server.OfferedSettings);
        Assert.Equal(server.OfferedSettings, client.AcceptedSettings);
        Assert.NotNull(client.LocalPlayer);
        Assert.Null(client.LastReject);
        Assert.Null(client.AcceptedJoin);
        Assert.Equal(RunPhase.Lobby, server.Session.Phase);
    }

    private static ClientRuntime[] ConnectSeats(LoopbackHub hub, int count, IStackCatalog? catalog)
    {
        var clients = new ClientRuntime[count];
        for (int i = 0; i < count; i++)
        {
            clients[i] = catalog is null ? new ClientRuntime() : new ClientRuntime(catalog);
            clients[i].Connect(hub.ClientEnds[i]);
        }

        return clients;
    }

    private static void ReceiveAll(ClientRuntime[] clients)
    {
        for (int i = 0; i < clients.Length; i++)
            clients[i].Receive();
    }

    private static void AssertContainerVersionsMatch(InventorySystem inventory, JoinState join)
    {
        int hostCount = 0;
        foreach (var container in inventory.Containers)
            hostCount++;

        Assert.Equal(hostCount, join.Containers.Count);
        for (int i = 0; i < join.Containers.Count; i++)
        {
            var stamp = join.Containers[i];
            Assert.True(inventory.TryGetContainer(stamp.Id, out var grid));
            Assert.Equal(grid.Version, stamp.Version);
            Assert.Equal(grid.Hash, stamp.Hash);
        }
    }

    private sealed class EventCatalog : IStackCatalog
    {
        public static readonly EventCatalog Instance = new();

        public Footprint FootprintOf(StackKey key)
        {
            if (key.IsMail && key.Def == MailKinds.Letter.Value)
                return new Footprint(1, 1);
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail && key.Def == MailKinds.Letter.Value)
                return 20;
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (key.IsMail && key.Def == MailKinds.Letter.Value)
                return WeightClass.Light;
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public StackCategory CategoryOf(StackKey key)
            => key.IsMail ? StackCategory.Mail : StackCategory.Material;
    }
}
