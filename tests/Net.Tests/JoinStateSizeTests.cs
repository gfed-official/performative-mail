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
        var world = TypicalContainers();
        var catalog = world.Inventory!.Catalog;
        var hub = LoopbackHub.ForSeats(3);
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(hub.ServerEnds),
            world,
            new WorldOffer(FixedSeed, GoldenWorldHash),
            offeredSettings: null,
            PrepOfShift3());

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
        Assert.Equal(GoldenWorldHash, server.OfferedWorld!.Value.WorldHash);
        AssertContainerHashesMatch(world.Inventory, joiner.AcceptedJoin.Value);
    }

    private static RunState PrepOfShift3()
    {
        var balance = BalanceCatalog.LoadFile(Path.Combine(FindContentRoot(), BalanceCatalog.RelativePath));
        var clock = new ShiftClock(balance, RunState.InLobby());
        if (!clock.TryEnter(RunPhase.Generating) || !clock.TryEnter(RunPhase.Prep))
            throw new InvalidOperationException("Could not enter Prep.");

        clock.Connect(1);
        for (byte shift = 1; shift <= 2; shift++)
        {
            clock.SetReady(1, true);
            if (clock.State.Phase != RunPhase.Delivery)
                throw new InvalidOperationException($"Ready did not start Delivery on shift {shift}.");
            clock.AdvanceTo(clock.State.PhaseDeadlineTick);
            if (clock.State.Phase == RunPhase.Raid)
                clock.AdvanceTo(clock.State.PhaseDeadlineTick);
            if (!clock.TryEnter(RunPhase.Draft) || !clock.TryAllPicked())
                throw new InvalidOperationException($"Could not reach next Prep after shift {shift}.");
        }

        if (clock.State.Phase != RunPhase.Prep || clock.State.Shift != 3)
            throw new InvalidOperationException("Clock did not land in Prep of shift 3.");
        return clock.State;
    }

    private static SimWorld TypicalContainers()
    {
        var world = new SimWorld(BotCatalog.Default);
        var intake = world.Inventory!.CreateContainer(ContainerSpec.Intake);
        var chest = world.Inventory.CreateContainer(ContainerSpec.Chest);
        var address = new AddressId(1, 4, 13, 0);
        Assert.IsType<Accepted>(world.Inventory.Apply(
            Actor.System,
            new Deposit(intake, MailStack.Single(MailKinds.Letter, address, new MailId(1)))));
        Assert.IsType<Accepted>(world.Inventory.Apply(
            Actor.System,
            new Deposit(chest, MailStack.Single(MailKinds.Letter, address, new MailId(2)))));
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

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, BalanceCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/balance.json");
    }
}
