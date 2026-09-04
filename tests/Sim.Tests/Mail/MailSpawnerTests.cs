using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Tests.Inventory;
using PerformativeMail.Sim.World;
using InventoryRejected = PerformativeMail.Sim.Inventory.Rejected;

namespace PerformativeMail.Sim.Tests.Mail;

public sealed class MailSpawnerTests
{
    private const int SeedA = 1;
    private const int SeedB = 99;
    private const uint FirstBatchTick = MailSpawnConstants.BatchIntervalTicks;
    private const uint Shift1Ticks = MailSpawnConstants.Shift1DeliveryTicks;

    [Fact]
    public void Step_After450TicksJitterPinned_DepositsRegistryMailToIntake()
    {
        var world = CreateWorld(SeedA);
        TickThrough(world, FirstBatchTick);

        var intake = IntakeGrid(world);
        Assert.True(intake.Entries.Count >= 1);
        var seen = new List<MailId>();
        foreach (var entry in intake.Entries)
        {
            var stack = Assert.IsType<MailStack>(entry.Stack);
            foreach (var id in stack.Ids)
            {
                Assert.True(world.Mail!.Contains(id));
                Assert.True(world.Mail.TryGet(id, out var item));
                Assert.Contains(item.Address, world.Atlas!.DeliverableAddresses);
                seen.Add(id);
            }
        }

        Assert.NotEmpty(seen);
    }

    [Fact]
    public void Step_SpawnedLetter_DeadlineEqualsSpawnShift()
    {
        var world = CreateWorld(SeedA);
        TickThrough(world, FirstBatchTick);

        Assert.Equal(MailSpawnConstants.LateValueRatio, world.MailSpawner!.LateValueRatio);
        Assert.Equal(0, MailKinds.DeadlineOffsetShifts(MailKinds.Letter));
        Assert.Equal(1, MailKinds.DeadlineOffsetShifts(MailKinds.Cargo));
        foreach (var item in world.Mail!.Items)
        {
            Assert.Equal(
                (byte)(item.SpawnShift + MailKinds.DeadlineOffsetShifts(item.Kind)),
                item.DeadlineShift);
        }
    }

    [Fact]
    public void Step_FullIntake_BacklogTickAddsOneComplaint()
    {
        var world = CreateWorld(SeedA);
        FillIntake(world);
        Assert.Equal(0, world.Complaint.Points);

        TickThrough(world, FirstBatchTick);

        Assert.NotEmpty(world.MailSpawner!.Backlog);
        Assert.Equal(ComplaintMeter.BacklogTick, world.Complaint.Points);
    }

    [Fact]
    public void Step_SameSeed_RepeatsIdsKindsAndAddresses()
    {
        var first = RunOrdered(SeedA, FirstBatchTick);
        var second = RunOrdered(SeedA, FirstBatchTick);
        Assert.Equal(first.Length, second.Length);
        for (int i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i].Id, second[i].Id);
            Assert.Equal(first[i].Kind, second[i].Kind);
            Assert.Equal(first[i].Address, second[i].Address);
        }
    }

    [Fact]
    public void Step_DifferentSeed_ChangesAddressStream()
    {
        var first = Addresses(RunOrdered(SeedA, FirstBatchTick));
        var second = Addresses(RunOrdered(SeedB, FirstBatchTick));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void StreetStreakPicker_TwoFakeStreets_AtLeastThirtyPercentShareAStreet()
    {
        var pool = new[]
        {
            new AddressId(1, 3, 1, 0),
            new AddressId(1, 3, 2, 0),
            new AddressId(1, 3, 3, 0),
            new AddressId(1, 7, 1, 0),
            new AddressId(1, 7, 2, 0),
            new AddressId(1, 7, 3, 0)
        };

        Assert.Equal(2, StreetStreakPicker.SharedCount(new[]
        {
            new AddressId(1, 3, 1, 0),
            new AddressId(1, 3, 9, 0),
            new AddressId(1, 7, 1, 0)
        }));

        const int count = 10;
        int need = StreetStreakPicker.MinimumShared(count, MailSpawnConstants.StreetStreakRatio);
        Assert.True(need * 1.0 / count >= MailSpawnConstants.StreetStreakRatio);

        for (int seed = 1; seed <= 20; seed++)
        {
            var batch = StreetStreakPicker.Pick(pool, count, MailSpawnConstants.StreetStreakRatio, new Random(seed));
            int shared = StreetStreakPicker.SharedCount(batch);
            Assert.True(shared >= need, $"seed {seed}: shared {shared} need {need}");
            foreach (var address in batch)
                Assert.Contains(address, pool);
        }
    }

    [Fact]
    public void Step_FullIntake_RejectedItemGoesToBacklogThenDepositsAfterWithdraw()
    {
        var world = CreateWorld(SeedA);
        FillIntake(world);
        var filledIds = IntakeMailIds(world);
        Assert.Equal(ContainerSpec.Intake.Shape.CellCount, filledIds.Count);

        TickThrough(world, FirstBatchTick);

        Assert.NotEmpty(world.MailSpawner!.Backlog);
        var head = world.MailSpawner.Backlog[0];
        Assert.True(world.Mail!.Contains(head.Id));
        Assert.DoesNotContain(head.Id.Value, IntakeMailIds(world));
        Assert.Equal(filledIds.Count, IntakeMailIds(world).Count);

        // Enough free cells for a shift-1 medium (2×2). chapter 03 §1.1
        for (int i = 0; i < 4; i++)
        {
            var occupant = FirstEntry(world);
            Assert.IsType<Accepted>(world.Inventory!.Apply(Actor.System, new Withdraw(world.Intake, occupant)));
        }

        world.Tick(FirstBatchTick + 1);

        Assert.Contains(head.Id.Value, IntakeMailIds(world));
        foreach (var leftover in world.MailSpawner.Backlog)
            Assert.NotEqual(head.Id, leftover.Id);
    }

    [Fact]
    public void Step_240sJitterPinned_SpawnedRegistryValueIs960IncludingBacklog()
    {
        Assert.Equal(16, MailSpawnConstants.BatchesPerShift);
        Assert.Equal(
            MailSpawnConstants.Shift1SpawnValueCents / MailSpawnConstants.BatchesPerShift,
            MailSpawnConstants.Shift1SpawnValueCents / (MailSpawnConstants.Shift1DeliverySeconds / MailSpawnConstants.BatchIntervalSeconds));

        var world = CreateWorld(SeedA);
        TickThrough(world, Shift1Ticks);

        int sum = 0;
        foreach (var item in world.Mail!.Items)
            sum += item.Value;

        Assert.Equal(MailSpawnConstants.Shift1SpawnValueCents, sum);
        Assert.Equal(MailSpawnConstants.Shift1SpawnValueCents, world.MailSpawner!.SpawnedValue);
        Assert.True(world.Mail.Count > 0);
    }

    [Fact]
    public void Deposit_PlayerIntoIntake_IsForbidden()
    {
        var world = CreateWorld(SeedA);
        var player = EntityId.FromClassAndCounter(1, 1);
        var address = world.Atlas!.DeliverableAddresses[0];
        var stack = MailStack.Single(MailKinds.Letter, address, new MailId(9001));

        var result = world.Inventory!.Apply(Actor.Player(player), new Deposit(world.Intake, stack));

        Assert.Equal(PerformativeMail.Sim.Inventory.RejectReason.Forbidden, Assert.IsType<InventoryRejected>(result).Reason);
    }

    [Fact]
    public void Deposit_NonMailIntoIntake_IsWrongCategory()
    {
        var world = CreateWorld(SeedA);
        var log = new ItemStack(TestStackCatalog.Log, 1);

        var result = world.Inventory!.Apply(Actor.System, new Deposit(world.Intake, log));

        Assert.Equal(PerformativeMail.Sim.Inventory.RejectReason.WrongCategory, Assert.IsType<InventoryRejected>(result).Reason);
    }

    private static SimWorld CreateWorld(int seed)
        => new(LoadRepoAtlas(), TestStackCatalog.Default, seed, jitterSeconds: 0);

    private static void TickThrough(SimWorld world, uint lastTick)
    {
        for (uint tick = 1; tick <= lastTick; tick++)
            world.Tick(tick);
    }

    private static GridContainer IntakeGrid(SimWorld world)
    {
        Assert.True(world.Inventory!.TryGetContainer(world.Intake, out var grid));
        return grid;
    }

    private static MailItem[] RunOrdered(int seed, uint lastTick)
    {
        var world = CreateWorld(seed);
        TickThrough(world, lastTick);
        var items = new List<MailItem>();
        foreach (var item in world.Mail!.Items)
            items.Add(item);
        items.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        return items.ToArray();
    }

    private static AddressId[] Addresses(MailItem[] items)
    {
        var addresses = new AddressId[items.Length];
        for (int i = 0; i < items.Length; i++)
            addresses[i] = items[i].Address;
        return addresses;
    }

    private static HashSet<uint> IntakeMailIds(SimWorld world)
    {
        var ids = new HashSet<uint>();
        foreach (var entry in IntakeGrid(world).Entries)
        {
            if (entry.Stack is not MailStack mail) continue;
            foreach (var id in mail.Ids)
                ids.Add(id.Value);
        }

        return ids;
    }

    private static EntryId FirstEntry(SimWorld world)
        => IntakeGrid(world).Entries.First().Id;

    private static void FillIntake(SimWorld world)
    {
        int cells = ContainerSpec.Intake.Shape.CellCount;
        for (int i = 0; i < cells; i++)
        {
            var address = new AddressId(2, (byte)(i / 256), (byte)i, 0);
            var stack = MailStack.Single(MailKinds.Letter, address, new MailId(10_000 + (uint)i));
            Assert.IsType<Accepted>(world.Inventory!.Apply(Actor.System, new Deposit(world.Intake, stack)));
        }
    }

    private static WorldAtlas LoadRepoAtlas()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content", "world", "m0_test_map.json");
                if (File.Exists(candidate))
                    return WorldAtlasLoader.LoadFile(Path.GetFullPath(candidate));
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/m0_test_map.json");
    }
}
