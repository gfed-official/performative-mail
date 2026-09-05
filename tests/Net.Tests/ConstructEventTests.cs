using System;
using System.IO;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class ConstructEventTests
{
    private static readonly TileCoord Origin = new(1, 1);
    private static readonly ItemDefId LogId = new(1);
    private static readonly EntityId FirstConstruct = EntityId.FromClassAndCounter(EntityClass.Construct, 1);
    private static readonly EntityId FirstPlayer = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    private static readonly byte[] PlaceRequestBytes =
    {
        0x3C,
        0x01, 0x00, 0x00, 0x00,
        0x09,
        0x77, 0x61, 0x6C, 0x6C, 0x5F, 0x77, 0x6F, 0x6F, 0x64,
        0x01, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x00,
        0x00,
    };

    private static readonly byte[] PlaceConfirmedBytes =
    {
        0x3D,
        0x01, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x03,
        0x09,
        0x77, 0x61, 0x6C, 0x6C, 0x5F, 0x77, 0x6F, 0x6F, 0x64,
        0x01, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x00,
        0x00,
        0x01, 0x00, 0x00, 0x01,
    };

    private static readonly byte[] RemoveRequestBytes =
    {
        0x3E,
        0x02, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x03,
    };

    private static readonly byte[] RemoveConfirmedBytes =
    {
        0x3F,
        0x02, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x03,
    };

    [Fact]
    public void MessageKind_PlaceAndRemove_AreSixtyThroughSixtyThree()
    {
        Assert.Equal(60, (byte)MessageKind.PlaceConstruct);
        Assert.Equal(61, (byte)MessageKind.PlaceConstructConfirmed);
        Assert.Equal(62, (byte)MessageKind.RemoveConstruct);
        Assert.Equal(63, (byte)MessageKind.RemoveConstructConfirmed);
        Assert.Equal(0x4112C9FAu, Protocol.SchemaHash);
    }

    [Fact]
    public void PlaceConstruct_GoldenRoundTrip()
    {
        var request = new PlaceConstructRequest(1, "wall_wood", 1, 1, Facing.North);
        Assert.Equal(PlaceRequestBytes, ConstructCodec.Encode(request));
        Assert.True(ConstructCodec.TryDecode(PlaceRequestBytes, out PlaceConstructRequest decoded));
        Assert.Equal(request, decoded);

        var confirmed = new PlaceConstructConfirmed(
            1,
            FirstConstruct,
            "wall_wood",
            1,
            1,
            Facing.North,
            FirstPlayer);
        Assert.Equal(PlaceConfirmedBytes, ConstructCodec.Encode(confirmed));
        Assert.True(ConstructCodec.TryDecode(PlaceConfirmedBytes, out PlaceConstructConfirmed seen));
        Assert.Equal(confirmed, seen);
    }

    [Fact]
    public void RemoveConstruct_GoldenRoundTrip()
    {
        var request = new RemoveConstructRequest(2, FirstConstruct);
        Assert.Equal(RemoveRequestBytes, ConstructCodec.Encode(request));
        Assert.True(ConstructCodec.TryDecode(RemoveRequestBytes, out RemoveConstructRequest decoded));
        Assert.Equal(request, decoded);

        var confirmed = new RemoveConstructConfirmed(2, FirstConstruct);
        Assert.Equal(RemoveConfirmedBytes, ConstructCodec.Encode(confirmed));
        Assert.True(ConstructCodec.TryDecode(RemoveConfirmedBytes, out RemoveConstructConfirmed seen));
        Assert.Equal(confirmed, seen);
    }

    [Fact]
    public void PlaceConstruct_TwoClients_SecondSeesConfirmedAndServerConsumed()
    {
        var fx = Hosted(logs: 3);

        fx.First.Connection!.Send(
            NetChannels.Reliable,
            ConstructCodec.Encode(new PlaceConstructRequest(1, "wall_wood", Origin.X, Origin.Y, Facing.North)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Equal(1, fx.World.Constructs!.Count);
        Assert.Equal(1, fx.Second.Constructs!.Count);
        Assert.Equal(1, fx.First.Constructs!.Count);
        Assert.True(fx.Second.Constructs.TryGet(FirstConstruct, out var seen));
        Assert.Equal("wall_wood", seen.DefId);
        Assert.Equal(Origin, seen.Tile);
        Assert.Equal(fx.First.LocalPlayer, seen.Owner);
        Assert.Equal(0, CountLog(fx.Inv, fx.Bag));
        Assert.Equal(3, CountLog(fx.SecondBagInv, fx.SecondBag));
    }

    [Fact]
    public void PlaceConstruct_RejectMissingInput_DoesNotSpawnOnSecondClient()
    {
        var fx = Hosted(logs: 0);

        fx.First.Connection!.Send(
            NetChannels.Reliable,
            ConstructCodec.Encode(new PlaceConstructRequest(1, "wall_wood", Origin.X, Origin.Y, Facing.North)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Equal(0, fx.World.Constructs!.Count);
        Assert.Equal(0, fx.First.Constructs!.Count);
        Assert.Equal(0, fx.Second.Constructs!.Count);
    }

    [Fact]
    public void PlaceConstruct_RejectOutOfRange_DoesNotSpawnOnSecondClient()
    {
        var fx = Hosted(logs: 3);
        var far = new TileCoord(10, 10);

        fx.First.Connection!.Send(
            NetChannels.Reliable,
            ConstructCodec.Encode(new PlaceConstructRequest(1, "wall_wood", far.X, far.Y, Facing.North)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Equal(0, fx.World.Constructs!.Count);
        Assert.Equal(0, fx.Second.Constructs!.Count);
        Assert.Equal(3, CountLog(fx.Inv, fx.Bag));
    }

    [Fact]
    public void RemoveConstruct_TwoClients_SecondLosesConstruct()
    {
        var fx = Hosted(logs: 3);
        fx.First.Connection!.Send(
            NetChannels.Reliable,
            ConstructCodec.Encode(new PlaceConstructRequest(1, "wall_wood", Origin.X, Origin.Y, Facing.North)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();
        Assert.Equal(1, fx.Second.Constructs!.Count);

        fx.First.Connection.Send(
            NetChannels.Reliable,
            ConstructCodec.Encode(new RemoveConstructRequest(2, FirstConstruct)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Equal(0, fx.World.Constructs!.Count);
        Assert.Equal(0, fx.First.Constructs!.Count);
        Assert.Equal(0, fx.Second.Constructs!.Count);
    }

    private static Fixture Hosted(int logs)
    {
        var catalog = new MaterialCatalog();
        var world = new SimWorld(catalog);
        var inv = world.Inventory!;
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        DepositChunks(inv, bag, logs);
        var secondInv = new InventorySystem(catalog);
        var secondBag = secondInv.CreateContainer(ContainerSpec.Chest);
        DepositChunks(secondInv, secondBag, 3);

        var buildings = BuildingCatalog.LoadDir(Path.Combine(ContentRoot.Find(), BuildingCatalog.RelativeDir));
        var recipes = RecipeCatalog.LoadDir(Path.Combine(ContentRoot.Find(), RecipeCatalog.RelativeDir));
        var ids = new Dictionary<string, ItemDefId>(StringComparer.Ordinal)
        {
            ["log"] = LogId,
            ["plank"] = new ItemDefId(2),
            ["iron_ingot"] = new ItemDefId(3)
        };
        world.Constructs = new ConstructRegistry(
            buildings,
            recipes,
            PlacementField.Flat(16, 16, 200),
            inv,
            bag,
            ids);

        var hub = LoopbackHub.ForSeats(2);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds), world);
        var first = new ClientRuntime(catalog)
        {
            Constructs = new ConstructRegistry(buildings, recipes, PlacementField.Flat(16, 16, 200), secondInv, secondBag, ids)
        };
        var second = new ClientRuntime(catalog)
        {
            Constructs = new ConstructRegistry(buildings, recipes, PlacementField.Flat(16, 16, 200), secondInv, secondBag, ids)
        };

        first.Connect(hub.ClientEnds[0]);
        second.Connect(hub.ClientEnds[1]);
        server.TickOnce();
        first.Receive();
        second.Receive();

        Assert.True(first.LocalPlayer.HasValue);
        Assert.True(world.Players.TryGet(first.LocalPlayer.Value, out var body));
        var hotbar = inv.CreateContainer(ContainerSpec.Hotbar, first.LocalPlayer.Value);
        server.BindPlayerBags(body, hotbar, bag);

        return new Fixture(world, server, first, second, inv, bag, secondInv, secondBag);
    }

    private static void DepositChunks(InventorySystem inv, ContainerId bag, int count)
    {
        int left = count;
        while (left > 0)
        {
            int n = left < 10 ? left : 10;
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(LogId, n))));
            left -= n;
        }
    }

    private static int CountLog(InventorySystem inv, ContainerId bag)
    {
        Assert.True(inv.TryGetContainer(bag, out var grid));
        int n = 0;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is ItemStack item && item.Item.Equals(LogId))
                n += item.Count;
        }

        return n;
    }

    private readonly record struct Fixture(
        SimWorld World,
        ServerRuntime Server,
        ClientRuntime First,
        ClientRuntime Second,
        InventorySystem Inv,
        ContainerId Bag,
        InventorySystem SecondBagInv,
        ContainerId SecondBag);

    private sealed class MaterialCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            if (key.Def == LogId.Value) return new Footprint(1, 2);
            return new Footprint(1, 1);
        }

        public int MaxStackOf(StackKey key) => 10;

        public WeightClass WeightOf(StackKey key) => WeightClass.Light;

        public StackCategory CategoryOf(StackKey key) => StackCategory.Material;
    }
}
