using System;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class InventoryEventTests
{
    private static readonly byte[] EmptyChestResetBytes =
    {
        0x28,
        0x01, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00,
        0x03,
        0x08, 0x04, 0x00,
        0x00, 0x00,
        0x00,
        0x00, 0x00,
        0x00,
    };

    private static readonly byte[] RemoveBytes =
    {
        0x28,
        0x07, 0x00, 0x00, 0x00,
        0x03, 0x00, 0x00, 0x00,
        0x04, 0x00, 0x00, 0x00,
        0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
        0x01, 0x00,
        0x02,
        0x09, 0x00, 0x00, 0x00,
        0x00,
    };

    [Fact]
    public void MessageKind_InventoryEvent_IsForty()
    {
        Assert.Equal(40, (byte)MessageKind.InventoryEvent);
        Assert.Equal(1, (byte)MessageKind.Hello);
        Assert.Equal(10, (byte)MessageKind.Input);
        Assert.Equal(20, (byte)MessageKind.Snapshot);
        Assert.Equal(30, (byte)MessageKind.Ping);
    }

    [Fact]
    public void EncodeEvent_EmptyChestReset_GoldenRoundTrip()
    {
        var delta = new ContainerDelta(
            new ContainerId(1),
            new ContainerVersion(0),
            new ContainerVersion(0),
            0,
            new Change[] { new Reset(ContainerSpec.Chest, Array.Empty<Entry>()) });

        Assert.Equal(EmptyChestResetBytes, InventoryCodec.EncodeEvent(delta));
        Assert.True(InventoryCodec.TryParseEvent(EmptyChestResetBytes, out var decoded, out var reqId));
        Assert.Null(reqId);
        Assert.Equal(delta.Container, decoded.Container);
        Assert.Equal(delta.BeforeVersion, decoded.BeforeVersion);
        Assert.Equal(delta.Version, decoded.Version);
        Assert.Equal(delta.Hash, decoded.Hash);
        var reset = Assert.IsType<Reset>(Assert.Single(decoded.Changes));
        Assert.Equal(8, reset.Spec.Shape.Cols);
        Assert.Equal(4, reset.Spec.Shape.Rows);
        Assert.False(reset.Spec.Shape.IgnoresFootprint);
        Assert.Null(reset.Spec.AllowedCategories);
        Assert.Empty(reset.Entries);
    }

    [Fact]
    public void EncodeEvent_Remove_GoldenRoundTrip()
    {
        var delta = new ContainerDelta(
            new ContainerId(7),
            new ContainerVersion(3),
            new ContainerVersion(4),
            0x0102030405060708UL,
            new Change[] { new Remove(new EntryId(9)) });

        Assert.Equal(RemoveBytes, InventoryCodec.EncodeEvent(delta));
        Assert.True(InventoryCodec.TryParseEvent(RemoveBytes, out var decoded, out var reqId));
        Assert.Null(reqId);
        var remove = Assert.IsType<Remove>(Assert.Single(decoded.Changes));
        Assert.Equal(9u, remove.Id.Value);
        Assert.Equal(0x0102030405060708UL, decoded.Hash);
    }

    [Fact]
    public void EncodeEvent_MailUpsertAndReqId_RoundTrip()
    {
        var address = new AddressId(1, 4, 13, 0);
        var stack = MailStack.Single(MailKinds.Letter, address, new MailId(9001));
        var entry = new Entry(new EntryId(5), stack, new Placement(2, 1, false));
        var delta = new ContainerDelta(
            new ContainerId(1),
            new ContainerVersion(0),
            new ContainerVersion(1),
            0xAABBCCDD11223344UL,
            new Change[] { new Upsert(entry) });

        var encoded = InventoryCodec.EncodeEvent(delta, 771);
        Assert.True(InventoryCodec.TryParseEvent(encoded, out var decoded, out var reqId));
        Assert.Equal(771u, reqId);
        var upsert = Assert.IsType<Upsert>(Assert.Single(decoded.Changes));
        Assert.Equal(5u, upsert.Entry.Id.Value);
        Assert.Equal(2, upsert.Entry.At.X);
        Assert.Equal(1, upsert.Entry.At.Y);
        var mail = Assert.IsType<MailStack>(upsert.Entry.Stack);
        Assert.Equal(MailKinds.Letter, mail.Kind);
        Assert.Equal(address, mail.Address);
        Assert.Equal(new[] { new MailId(9001) }, mail.Ids);
    }

    [Fact]
    public void EncodeEvent_HotbarBlockedAndIntakeAllowList_RoundTrip()
    {
        var hotbar = new ContainerDelta(
            new ContainerId(2),
            new ContainerVersion(0),
            new ContainerVersion(0),
            0,
            new Change[] { new Reset(ContainerSpec.Hotbar, Array.Empty<Entry>()) });
        Assert.True(InventoryCodec.TryParseEvent(InventoryCodec.EncodeEvent(hotbar), out var decodedHotbar, out _));
        var hotbarReset = Assert.IsType<Reset>(Assert.Single(decodedHotbar.Changes));
        Assert.True(hotbarReset.Spec.Shape.IsBlocked(new Cell(0, 0)));
        Assert.False(hotbarReset.Spec.Shape.IsBlocked(new Cell(1, 0)));

        var intake = new ContainerDelta(
            new ContainerId(3),
            new ContainerVersion(0),
            new ContainerVersion(0),
            0,
            new Change[] { new Reset(ContainerSpec.Intake, Array.Empty<Entry>()) });
        Assert.True(InventoryCodec.TryParseEvent(InventoryCodec.EncodeEvent(intake), out var decodedIntake, out _));
        var intakeReset = Assert.IsType<Reset>(Assert.Single(decodedIntake.Changes));
        Assert.Equal(20, intakeReset.Spec.Shape.Cols);
        Assert.Equal(16, intakeReset.Spec.Shape.Rows);
        Assert.NotNull(intakeReset.Spec.AllowedCategories);
        Assert.Contains(StackCategory.Mail, intakeReset.Spec.AllowedCategories);
        Assert.False(intakeReset.Spec.Accepts(StackCategory.Material));
    }

    [Fact]
    public void EncodeEvent_TruncatedAndTrailingBytes_FailDecode()
    {
        Assert.False(InventoryCodec.TryParseEvent(EmptyChestResetBytes.AsSpan(0, EmptyChestResetBytes.Length - 1), out _, out _));
        Assert.False(InventoryCodec.TryParseEvent(ReadOnlySpan<byte>.Empty, out _, out _));
        Assert.False(InventoryCodec.TryParseEvent(new byte[] { 0x14 }, out _, out _));

        var trailing = new byte[EmptyChestResetBytes.Length + 1];
        Array.Copy(EmptyChestResetBytes, trailing, EmptyChestResetBytes.Length);
        Assert.False(InventoryCodec.TryParseEvent(trailing, out _, out _));
    }

    [Fact]
    public void TickOnce_SendsCommittedDeltaOnChannel1_ToViewers()
    {
        var catalog = EventCatalog.Instance;
        var hub = LoopbackHub.ForSeats(1);
        var world = new SimWorld(catalog);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds), world);
        var client = new ClientRuntime(catalog);
        client.Connect(hub.ClientEnds[0]);
        server.TickOnce();
        client.Receive();

        Assert.True(client.LocalPlayer.HasValue);
        var chest = world.Inventory!.CreateContainer(ContainerSpec.Chest);
        Assert.IsType<Accepted>(world.Inventory.Open(client.LocalPlayer.Value, chest));
        var accepted = Assert.IsType<Accepted>(world.Inventory.Apply(
            Actor.System,
            new Deposit(chest, MailStack.Single(MailKinds.Letter, new AddressId(1, 4, 13, 0), new MailId(1)))));
        Assert.NotEmpty(accepted.Deltas);

        server.TickOnce();

        var sawEvent = false;
        while (hub.ClientEnds[0].Poll(out var channel, out var payload))
        {
            if (channel != 1)
                continue;
            Assert.True(InventoryCodec.TryParseEvent(payload, out var delta, out _));
            Assert.Equal(chest, delta.Container);
            sawEvent = true;
        }

        Assert.True(sawEvent);
    }

    [Fact]
    public void ContainerDeltaEvent_TwoClients_OpenAndDeposit_ReplicasMatchServerHashAndVersion()
    {
        var catalog = EventCatalog.Instance;
        var hub = LoopbackHub.ForSeats(2);
        var world = new SimWorld(catalog);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds), world);

        var first = new ClientRuntime(catalog);
        var second = new ClientRuntime(catalog);
        first.Connect(hub.ClientEnds[0]);
        second.Connect(hub.ClientEnds[1]);

        server.TickOnce();
        first.Receive();
        second.Receive();

        Assert.True(first.LocalPlayer.HasValue);
        Assert.True(second.LocalPlayer.HasValue);
        Assert.NotEqual(first.LocalPlayer.Value, second.LocalPlayer.Value);

        var chest = world.Inventory!.CreateContainer(ContainerSpec.Chest);
        Assert.IsType<Accepted>(world.Inventory.Open(first.LocalPlayer.Value, chest));
        Assert.IsType<Accepted>(world.Inventory.Open(second.LocalPlayer.Value, chest));

        server.TickOnce();
        first.Receive();
        second.Receive();

        var accepted = Assert.IsType<Accepted>(world.Inventory.Apply(
            Actor.System,
            new Deposit(chest, MailStack.Single(MailKinds.Letter, new AddressId(1, 4, 13, 0), new MailId(42)))));
        Assert.NotEmpty(accepted.Deltas);

        server.TickOnce();
        first.Receive();
        second.Receive();

        Assert.True(world.Inventory.TryGetContainer(chest, out var authoritative));
        Assert.True(authoritative.Version.Value > 0);
        AssertReplica(first, chest, authoritative);
        AssertReplica(second, chest, authoritative);
        Assert.True(first.InventoryEventCount >= 2);
        Assert.True(second.InventoryEventCount >= 2);
    }

    [Fact]
    public void ContainerDeltaEvent_TwoClients_TakeFromIntake_ReplicasMatchServerHashAndVersion()
    {
        var catalog = EventCatalog.Instance;
        var hub = LoopbackHub.ForSeats(2);
        var world = new SimWorld(catalog);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds), world);

        var first = new ClientRuntime(catalog);
        var second = new ClientRuntime(catalog);
        first.Connect(hub.ClientEnds[0]);
        second.Connect(hub.ClientEnds[1]);

        server.TickOnce();
        first.Receive();
        second.Receive();

        Assert.True(first.LocalPlayer.HasValue);
        Assert.True(second.LocalPlayer.HasValue);

        var inv = world.Inventory!;
        var intake = inv.CreateContainer(ContainerSpec.Intake);
        var bag = inv.CreateContainer(ContainerSpec.BaseInventory, first.LocalPlayer.Value);
        Assert.IsType<Accepted>(inv.Open(first.LocalPlayer.Value, intake));
        Assert.IsType<Accepted>(inv.Open(second.LocalPlayer.Value, intake));
        Assert.IsType<Accepted>(inv.Apply(
            Actor.System,
            new Deposit(intake, MailStack.Single(MailKinds.Letter, new AddressId(1, 4, 13, 0), new MailId(7)))));

        server.TickOnce();
        first.Receive();
        second.Receive();

        var entry = FirstEntry(inv, intake);
        Assert.IsType<Accepted>(inv.Apply(
            Actor.Player(first.LocalPlayer.Value),
            new QuickMove(intake, entry, bag)));

        server.TickOnce();
        first.Receive();
        second.Receive();

        Assert.True(inv.TryGetContainer(intake, out var intakeAuth));
        Assert.True(intakeAuth.Version.Value > 0);
        AssertReplica(first, intake, intakeAuth);
        AssertReplica(second, intake, intakeAuth);
        Assert.True(first.InventoryEventCount >= 2);
        Assert.True(second.InventoryEventCount >= 2);
    }

    private static EntryId FirstEntry(InventorySystem inv, ContainerId container)
    {
        Assert.True(inv.TryGetContainer(container, out var grid));
        foreach (var entry in grid.Entries)
            return entry.Id;
        throw new InvalidOperationException("Container has no entries.");
    }

    private static void AssertReplica(ClientRuntime client, ContainerId chest, GridContainer authoritative)
    {
        Assert.NotNull(client.Inventory);
        Assert.True(client.Inventory.TryGetContainer(chest, out var replica));
        Assert.Equal(authoritative.Hash, replica.Hash);
        Assert.Equal(authoritative.Version, replica.Version);
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
