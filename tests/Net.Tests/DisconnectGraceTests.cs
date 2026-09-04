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

public sealed class DisconnectGraceTests
{
    [Fact]
    public void CloseGuest_HoldsBodyUntilOneHundredTwentySeconds()
    {
        var hub = LoopbackHub.ForSeats(2);
        var link = LoopbackLink.OverPipes(hub.ServerEnds);
        var server = new ServerRuntime(link);
        var host = new ClientRuntime();
        var guest = new ClientRuntime();
        host.Connect(hub.ClientEnds[0], 1);
        guest.Connect(hub.ClientEnds[1], 2);
        server.TickOnce();
        host.Receive();
        guest.Receive();
        Assert.Equal(2, server.World.Players.Count);

        link.Close(new ConnectionId(1), DisconnectReason.PeerLeft);
        server.TickOnce();
        host.Receive();
        Assert.Equal(2, server.World.Players.Count);
        Assert.Equal(1, server.Grace.HeldCount);

        for (int i = 0; i < DisconnectGrace.HoldTicks - 1; i++)
            server.TickOnce();
        Assert.Equal(2, server.World.Players.Count);

        server.TickOnce();
        host.Receive();
        Assert.Equal(1, server.World.Players.Count);
        Assert.Equal(0, server.Grace.HeldCount);
        Assert.Equal(0, host.RemoteCount);
    }

    [Fact]
    public void ReconnectWithinGrace_RestoresSameEntity()
    {
        var hub = LoopbackHub.ForSeats(2);
        var link = LoopbackLink.OverPipes(hub.ServerEnds);
        var server = new ServerRuntime(link);
        var host = new ClientRuntime();
        var guest = new ClientRuntime();
        host.Connect(hub.ClientEnds[0], 1);
        guest.Connect(hub.ClientEnds[1], 2);
        server.TickOnce();
        host.Receive();
        guest.Receive();
        var held = guest.LocalPlayer;
        Assert.True(held.HasValue);

        link.Close(new ConnectionId(1), DisconnectReason.PeerLeft);
        server.TickOnce();

        var pair = new LoopbackTransport();
        link.Accept(pair.A);
        var returning = new ClientRuntime();
        returning.Connect(pair.B, 2);
        server.TickOnce();
        returning.Receive();

        Assert.Null(returning.LastReject);
        Assert.Equal(held, returning.LocalPlayer);
        Assert.Equal(2, server.World.Players.Count);
        Assert.Equal(0, server.Grace.HeldCount);
        Assert.False(server.EndedWithoutResults);
    }

    [Fact]
    public void AfterGrace_DropsInventoryIntoDeathBag()
    {
        var catalog = LetterCatalog.Instance;
        var world = new SimWorld(catalog);
        var hub = LoopbackHub.ForSeats(2);
        var link = LoopbackLink.OverPipes(hub.ServerEnds);
        var server = new ServerRuntime(link, world);
        var host = new ClientRuntime();
        var guest = new ClientRuntime();
        host.Connect(hub.ClientEnds[0], 1);
        guest.Connect(hub.ClientEnds[1], 2);
        server.TickOnce();
        host.Receive();
        guest.Receive();

        Assert.True(guest.LocalPlayer.HasValue);
        Assert.True(world.Players.TryGet(guest.LocalPlayer.Value, out var body));
        var hotbar = world.Inventory!.CreateContainer(ContainerSpec.Hotbar, body.Id);
        var inventory = world.Inventory.CreateContainer(ContainerSpec.BaseInventory, body.Id);
        var accepted = Assert.IsType<Accepted>(world.Inventory.Apply(
            Actor.System,
            new Deposit(inventory, MailStack.Single(MailKinds.Letter, new AddressId(1, 4, 13, 0), new MailId(1)))));
        Assert.NotEmpty(accepted.Deltas);
        server.BindPlayerBags(body, hotbar, inventory);

        link.Close(new ConnectionId(1), DisconnectReason.PeerLeft);
        server.TickOnce();
        Assert.True(world.Inventory.TryGetContainer(inventory, out var still));
        Assert.Single(still.Entries);

        for (int i = 0; i < DisconnectGrace.HoldTicks; i++)
            server.TickOnce();

        Assert.False(world.Players.TryGet(body.Id, out _));
        Assert.True(server.Deaths!.TryGetBag(body.Id, out var bag));
        Assert.Single(server.Deaths.StacksIn(bag));
        Assert.Empty(Grid(world.Inventory, inventory).Entries);
    }

    [Fact]
    public void AllDisconnect_EndsWithoutResultsAfterSixtySeconds()
    {
        var loopback = new LoopbackTransport();
        var link = LoopbackLink.OverPipes(loopback.A);
        var server = new ServerRuntime(link);
        var host = new ClientRuntime();
        host.Connect(loopback.B, 1);
        server.TickOnce();
        host.Receive();
        Assert.Equal(1, server.World.Players.Count);

        link.Close(ConnectionId.HostSeat, DisconnectReason.PeerLeft);
        server.TickOnce();
        Assert.False(server.EndedWithoutResults);
        Assert.Equal(1, server.World.Players.Count);

        for (int i = 0; i < DisconnectGrace.EmptyTicks - 1; i++)
            server.TickOnce();
        Assert.False(server.EndedWithoutResults);

        server.TickOnce();
        Assert.True(server.EndedWithoutResults);
        Assert.Equal(RunPhase.Lobby, server.Session.Phase);
    }

    [Fact]
    public void DeliveryHello_Resume_SkipsWrongPhase()
    {
        var world = new SimWorld();
        var body = world.SpawnPlayer();
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(loopback.A),
            world,
            offeredWorld: null,
            offeredSettings: null,
            new RunState(RunPhase.Delivery, 1, 0));
        Assert.True(server.Grace.Hold(2, body.Id, 0, connectedAfter: 0));

        var returning = new ClientRuntime();
        returning.Connect(loopback.B, 2);
        server.TickOnce();
        returning.Receive();

        Assert.Null(returning.LastReject);
        Assert.Equal(body.Id, returning.LocalPlayer);
        Assert.Equal(1, server.World.Players.Count);
        Assert.Equal(0, server.Grace.HeldCount);
        Assert.Equal(RunPhase.Delivery, server.Session.Phase);
    }

    private static GridContainer Grid(InventorySystem inventory, ContainerId id)
    {
        Assert.True(inventory.TryGetContainer(id, out var grid));
        return grid;
    }

    private sealed class LetterCatalog : IStackCatalog
    {
        public static readonly LetterCatalog Instance = new();

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
