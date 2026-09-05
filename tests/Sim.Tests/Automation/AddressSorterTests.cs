using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class AddressSorterTests
{
    private static readonly TileCoord Origin = new(4, 4);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly ItemDefId StoneId = new(4);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly MailKindId[] Kinds =
    {
        MailKinds.Letter,
        MailKinds.Postcard,
        MailKinds.SmallPackage,
        MailKinds.MediumPackage,
        MailKinds.LargePackage
    };

    [Fact]
    public void RepoDefs_AddressSorterMk1_MatchesChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue(AddressSorter.BuildingId, out var sorter));
        Assert.True(recipes.TryGetValue(sorter.Recipe, out var recipe));
        Assert.Equal("recipe_address_sorter_mk1", sorter.Recipe);
        Assert.Equal(AddressSorter.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(500, sorter.Hp);
        Assert.Equal(2, sorter.Footprint.W);
        Assert.Equal(2, sorter.Footprint.H);
        Assert.Equal(4, sorter.Rotations);
        Assert.False(sorter.OnStreet);
        Assert.Equal(WaterPlacement.None, sorter.OnWater);
        Assert.Equal(15, sorter.MaxSlopeDeg);
        Assert.False(sorter.DragLine);
        Assert.Equal(BuildingBehaviour.Sorter, sorter.Behaviour);
        Assert.Null(sorter.Container);
        Assert.Null(recipe.Blueprint);
        Assert.Equal("iron_ingot", recipe.Inputs[0].Item);
        Assert.Equal(4, recipe.Inputs[0].Count);
        Assert.Equal("stone", recipe.Inputs[1].Item);
        Assert.Equal(6, recipe.Inputs[1].Count);
        Assert.Equal(8, AddressSorter.BufferSlots);
        Assert.Equal(1, AddressSorter.FilterSlotsPerOutput);
        Assert.Equal(15, AddressSorter.ExaminePeriodTicks);
    }

    [Fact]
    public void Place_ConsumesRecipe_AndOccupiesFourTiles()
    {
        var fx = Loaded(iron: 4, stone: 6);
        var placed = Assert.IsType<Placed>(
            fx.Registry.TryPlace(AddressSorter.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(AddressSorter.BuildingId, placed.Construct.DefId);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, IronId));
        Assert.Equal(0, CountItem(fx, StoneId));
        Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(AddressSorter.BuildingId, Origin, Facing.East, Owner));
        Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(AddressSorter.BuildingId, new TileCoord(5, 5), Facing.North, Owner));
    }

    [Fact]
    public void Place_StreetTile_Rejects()
    {
        var fx = Loaded(iron: 4, stone: 6, field: PlacementField.Flat(8, 8, 200).WithStreet(Origin));
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(AddressSorter.BuildingId, Origin, Facing.East, Owner));
        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(4, CountItem(fx, IronId));
        Assert.Equal(6, CountItem(fx, StoneId));
    }

    [Fact]
    public void Examine_WaitsHalfSecond_ThenEmitsToFirstMatch()
    {
        var sorter = new AddressSorter(Origin, Facing.East);
        sorter.SetFilter(SorterOutput.Left, AddressFilter.ForStreet(4));
        var item = Letter(1, 1, 4, 13, 0);
        Assert.True(sorter.TryAccept(item));

        sorter.StepTicks(AddressSorter.ExaminePeriodTicks - 1);
        Assert.Equal(1, sorter.BufferCount);
        Assert.Empty(sorter.Emitted(SorterOutput.Left));

        sorter.Step();
        Assert.Equal(0, sorter.BufferCount);
        Assert.Equal(item.ItemId, Assert.Single(sorter.Emitted(SorterOutput.Left)).ItemId);
        Assert.Empty(sorter.Emitted(SorterOutput.Overflow));
    }

    [Fact]
    public void Route_Unmatched_GoesToOverflow()
    {
        var sorter = new AddressSorter(Origin, Facing.East);
        sorter.SetFilter(SorterOutput.Forward, AddressFilter.ForStreet(2));
        var item = Letter(2, 1, 9, 1, 0);
        Assert.Equal(SorterOutput.Overflow, sorter.Route(item));
        Assert.True(sorter.TryAccept(item));
        sorter.StepTicks(AddressSorter.ExaminePeriodTicks);
        Assert.Equal(item.ItemId, Assert.Single(sorter.Emitted(SorterOutput.Overflow)).ItemId);
        Assert.Empty(sorter.Emitted(SorterOutput.Forward));
    }

    [Fact]
    public void Route_FirstMatchingOutputWins()
    {
        var sorter = new AddressSorter(Origin, Facing.East);
        var both = AddressFilter.ForDistrict(1);
        sorter.SetFilter(SorterOutput.Left, both);
        sorter.SetFilter(SorterOutput.Forward, both);
        Assert.Equal(SorterOutput.Left, sorter.Route(Letter(3, 1, 2, 3, 0)));
    }

    [Fact]
    public void Filter_DistrictKindUnitAndNumberRange_Match()
    {
        var sorter = new AddressSorter(Origin, Facing.East);
        sorter.SetFilter(SorterOutput.Left, AddressFilter.ForDistrict(2));
        sorter.SetFilter(SorterOutput.Forward, AddressFilter.ForKind(MailKinds.Postcard));
        sorter.SetFilter(SorterOutput.Right, new AddressFilter(NumberMin: 10, NumberMax: 19, Unit: 3));

        Assert.Equal(SorterOutput.Left, sorter.Route(Letter(4, 2, 1, 1, 0)));
        Assert.Equal(SorterOutput.Forward, sorter.Route(Item(5, MailKinds.Postcard, 1, 1, 1, 0)));
        Assert.Equal(SorterOutput.Right, sorter.Route(Letter(6, 1, 1, 15, 3)));
        Assert.Equal(SorterOutput.Overflow, sorter.Route(Letter(7, 1, 1, 15, 2)));
        Assert.Equal(SorterOutput.Overflow, sorter.Route(Letter(8, 1, 1, 9, 3)));
    }

    [Fact]
    public void Buffer_EighthItemAccepted_NinthRejected()
    {
        var sorter = new AddressSorter(Origin, Facing.East);
        for (int i = 0; i < AddressSorter.BufferSlots; i++)
            Assert.True(sorter.TryAccept(Letter(10 + i, 1, 1, (byte)i, 0)));
        Assert.False(sorter.TryAccept(Letter(99, 1, 1, 9, 0)));
        Assert.Equal(AddressSorter.BufferSlots, sorter.BufferCount);
    }

    [Fact]
    public void Route_OneThousandStreetFilteredItems_ZeroMisroutes()
    {
        var sorter = new AddressSorter(Origin, Facing.East);
        sorter.SetFilter(SorterOutput.Left, AddressFilter.ForStreet(1));
        sorter.SetFilter(SorterOutput.Forward, AddressFilter.ForStreet(2));
        sorter.SetFilter(SorterOutput.Right, AddressFilter.ForStreet(3));

        const int n = 1000;
        var items = new BeltItem[n];
        var expected = new SorterOutput[n];
        var want = new int[4];
        for (int i = 0; i < n; i++)
        {
            byte street = (byte)(1 + i % 5);
            var kind = Kinds[i % Kinds.Length];
            items[i] = Item(i + 1, kind, (byte)(1 + i % 3), street, (byte)(1 + i % 40), (byte)(i % 4));
            expected[i] = street switch
            {
                1 => SorterOutput.Left,
                2 => SorterOutput.Forward,
                3 => SorterOutput.Right,
                _ => SorterOutput.Overflow
            };
            want[(int)expected[i]]++;
        }

        int next = 0;
        int ticks = 0;
        while (next < n || sorter.BufferCount > 0)
        {
            while (next < n && sorter.TryAccept(items[next]))
                next++;
            sorter.Step();
            ticks++;
            Assert.True(ticks < 100_000);
        }

        Assert.Equal(n, next);
        Assert.Equal(0, sorter.BufferCount);
        Assert.Equal(want[(int)SorterOutput.Left], sorter.Emitted(SorterOutput.Left).Count);
        Assert.Equal(want[(int)SorterOutput.Forward], sorter.Emitted(SorterOutput.Forward).Count);
        Assert.Equal(want[(int)SorterOutput.Right], sorter.Emitted(SorterOutput.Right).Count);
        Assert.Equal(want[(int)SorterOutput.Overflow], sorter.Emitted(SorterOutput.Overflow).Count);
        Assert.Equal(want[(int)SorterOutput.Overflow], OverflowUnmatched(items, expected));

        var seen = new HashSet<int>();
        CheckPort(sorter, SorterOutput.Left, items, expected, seen);
        CheckPort(sorter, SorterOutput.Forward, items, expected, seen);
        CheckPort(sorter, SorterOutput.Right, items, expected, seen);
        CheckPort(sorter, SorterOutput.Overflow, items, expected, seen);
        Assert.Equal(n, seen.Count);
    }

    [Fact]
    public void Compile_BeltIntoSorter_MarksInputAndRoutesStreet()
    {
        var fx = Loaded(planks: 5, iron: 9, stone: 6, field: PlacementField.Flat(12, 10, 200));
        Assert.IsType<Placed>(fx.Registry.TryPlace(AddressSorter.BuildingId, Origin, Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(3, 4), Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(6, 4), Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(4, 6), Facing.North, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(4, 3), Facing.South, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(3, 5), Facing.West, Owner));

        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var sorters = new AddressSorterNetwork();
        sorters.Compile(fx.Registry.All, belts);
        var machine = Assert.Single(sorters.Sorters);
        Assert.True(sorters.SetFilter(Origin, SorterOutput.Forward, AddressFilter.ForStreet(2)));
        Assert.True(sorters.SetFilter(Origin, SorterOutput.Left, AddressFilter.ForStreet(1)));

        var input = SegmentEndingAt(belts, machine.Ports.Input);
        Assert.True(input.FeedsJunction);
        var oak = Letter(21, 1, 2, 8, 0);
        var elm = Letter(22, 1, 7, 1, 0);
        Assert.True(input.TryInsert(0, oak.ItemId, 2f, oak.Kind, oak.Address));
        Assert.True(input.TryInsert(1, elm.ItemId, 2f, elm.Kind, elm.Address));

        sorters.StepTicks(belts, AddressSorter.ExaminePeriodTicks);
        var forward = SegmentStartingAt(belts, machine.Ports.Neighbor(SorterOutput.Forward), Facing.East);
        Assert.Equal(oak.ItemId, Assert.Single(forward.Lane(0)).ItemId);
        Assert.Empty(machine.Emitted(SorterOutput.Overflow));

        sorters.StepTicks(belts, AddressSorter.ExaminePeriodTicks);
        var overflow = SegmentStartingAt(belts, machine.Ports.Neighbor(SorterOutput.Overflow), Facing.West);
        Assert.Equal(elm.ItemId, Assert.Single(overflow.Lane(0)).ItemId);
        Assert.Empty(input.Lane(0));
        Assert.Empty(input.Lane(1));
    }

    private static int OverflowUnmatched(BeltItem[] items, SorterOutput[] expected)
    {
        int n = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (expected[i] == SorterOutput.Overflow)
                n++;
        }

        return n;
    }

    private static void CheckPort(
        AddressSorter sorter,
        SorterOutput dest,
        BeltItem[] items,
        SorterOutput[] expected,
        HashSet<int> seen)
    {
        var emitted = sorter.Emitted(dest);
        for (int i = 0; i < emitted.Count; i++)
        {
            var item = emitted[i];
            Assert.True(seen.Add(item.ItemId));
            int index = item.ItemId - 1;
            Assert.Equal(dest, expected[index]);
            if (dest != SorterOutput.Overflow)
                Assert.True(sorter.Filter(dest).Matches(item));
            else
            {
                Assert.False(sorter.Filter(SorterOutput.Left).Matches(item));
                Assert.False(sorter.Filter(SorterOutput.Forward).Matches(item));
                Assert.False(sorter.Filter(SorterOutput.Right).Matches(item));
            }

            Assert.Equal(items[index].Address, item.Address);
            Assert.Equal(items[index].Kind, item.Kind);
        }
    }

    private static BeltItem Letter(int id, byte district, byte street, byte number, byte unit)
        => Item(id, MailKinds.Letter, district, street, number, unit);

    private static BeltItem Item(int id, MailKindId kind, byte district, byte street, byte number, byte unit)
        => new(id, 0f, kind, new AddressId(district, street, number, unit));

    private static BeltSegment SegmentEndingAt(BeltNetwork belts, TileCoord ahead)
    {
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            if (belts.Segments[i].AheadTile.Equals(ahead))
                return belts.Segments[i];
        }

        throw new InvalidOperationException("missing input segment");
    }

    private static BeltSegment SegmentStartingAt(BeltNetwork belts, TileCoord tile, Facing facing)
    {
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            var segment = belts.Segments[i];
            if (segment.Tiles.Count > 0 && segment.Tiles[0].Equals(tile) && segment.Facing == facing)
                return segment;
        }

        throw new InvalidOperationException("missing output segment");
    }

    private static Fixture Loaded(int planks = 0, int iron = 0, int stone = 0, PlacementField? field = null)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (planks > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, planks))));
        if (iron > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, iron))));
        if (stone > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(StoneId, stone))));
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field ?? PlacementField.Flat(12, 10, 200),
            inv,
            bag,
            Ids());
        return new Fixture(registry, inv, bag);
    }

    private static BuildingDef[] LoadBuildings() =>
        BuildingCatalog.LoadDir(Path.Combine(FindContentRoot(), BuildingCatalog.RelativeDir));

    private static RecipeDef[] LoadRecipes() =>
        RecipeCatalog.LoadDir(Path.Combine(FindContentRoot(), RecipeCatalog.RelativeDir));

    private static Dictionary<string, T> Index<T>(T[] defs) where T : class
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            string id = def switch
            {
                BuildingDef building => building.Id,
                RecipeDef recipe => recipe.Id,
                _ => throw new InvalidOperationException(def.GetType().Name)
            };
            map.Add(id, def);
        }

        return map;
    }

    private static Dictionary<string, ItemDefId> Ids() => new(StringComparer.Ordinal)
    {
        ["plank"] = PlankId,
        ["iron_ingot"] = IronId,
        ["stone"] = StoneId
    };

    private static int CountItem(Fixture fx, ItemDefId id)
    {
        Assert.True(fx.Inv.TryGetContainer(fx.Bag, out var grid));
        int n = 0;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is ItemStack item && item.Item.Equals(id))
                n += item.Count;
        }

        return n;
    }

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, ArchetypeCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/archetypes.json");
    }

    private readonly record struct Fixture(ConstructRegistry Registry, InventorySystem Inv, ContainerId Bag);

    private sealed class MaterialCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return new Footprint(1, 1);
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return 20;
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return WeightClass.Light;
        }

        public StackCategory CategoryOf(StackKey key) => StackCategory.Material;
    }
}
