using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Tests.World;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class ReferenceFactoryTests
{
    private const string PlankSku = "plank_x20";
    private const string IronSku = "iron_ingot_x10";
    private const int PlankBuys = 3;
    private const int IronBuys = 6;
    private const int StoneFromHarvestMin = 12;
    private const int PlankFromShopMin = 20;
    private const int IronFromShopMin = 40;
    private const int StepTicks = 8;
    private const float InsertMetres = 0.25f;

    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly int PickaxeSwingTicks = TickClock.TickHz * 3 / 5;
    private static readonly int PlaceInteractTicks = TickClock.TickHz * 2 / 5;
    private static readonly int ShopBuyTicks = TickClock.TickHz;

    [Fact]
    public void SmallIsland_ShopAndHarvest_PlacesAndRunsWithinPrepPlusDelivery()
    {
        var bundle = ContentFiles.Load(FindContentRoot());
        var ids = ContentIdMap.Build(bundle);
        var catalog = ContentStackCatalog.From(bundle, ids);
        Assert.True(ids.TryItem("plank", out var plankId));
        Assert.True(ids.TryItem("iron_ingot", out var ironId));
        Assert.True(ids.TryItem("stone", out var stoneId));

        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        var field = PlacementField.FromWorld(tables);
        var scanField = PlacementField.FromWorld(tables);
        var occupied = OccupiedMask(tables);
        var nodes = ResourceTiles(tables);
        var (origin, split, layout) = Sit(tables, scanField, occupied, nodes);

        var bill = BillOf(layout, bundle.Buildings, bundle.Recipes);
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Depot);
        var registry = new ConstructRegistry(
            bundle.Buildings,
            bundle.Recipes,
            field,
            inv,
            bag,
            ids.Items);
        var wallet = new Wallet(new Cents(3000));
        var shop = new ShopSession(bundle.Shop, wallet, WorldGenHashTests.FixedSeed, inv, bag, ids.Items);
        var harvest = new HarvestSession(tables.ResourceNodes, inv, bag, ids.Items);
        var clock = new ShiftClock(bundle.Balance, RunState.InLobby());
        Assert.True(clock.TryEnter(RunPhase.Generating));
        Assert.True(clock.TryEnter(RunPhase.Prep));
        int prepTicks = ShiftDurations.Ticks(RunPhase.Prep, 1, bundle.Balance);
        int deliveryTicks = ShiftDurations.Ticks(RunPhase.Delivery, 1, bundle.Balance);

        shop.RollOffers(1, RunPhase.Prep);
        int plankGranted = Buy(shop, clock, PlankSku, PlankBuys);
        int ironGranted = Buy(shop, clock, IronSku, IronBuys);
        Assert.True(plankGranted >= PlankFromShopMin);
        Assert.True(ironGranted >= IronFromShopMin);

        var used = TilesOf(layout);
        var stoneTile = FirstStone(tables, used);
        int stoneGranted = 0;
        while (stoneGranted < StoneFromHarvestMin)
        {
            var hit = Assert.IsType<Harvested>(harvest.Hit(stoneTile, HarvestTool.Pickaxe));
            stoneGranted += hit.Count;
            Charge(clock, PickaxeSwingTicks);
        }

        Assert.True(stoneGranted >= StoneFromHarvestMin);
        int plankBefore = CountItem(inv, bag, plankId);
        int ironBefore = CountItem(inv, bag, ironId);
        int stoneBefore = CountItem(inv, bag, stoneId);

        foreach (var row in layout)
        {
            var result = registry.TryPlace(row.DefId, row.Tile, row.Rotation, Owner);
            if (result is PlaceRejected rejected)
                Assert.Fail($"{rejected.Reason} at ({row.Tile.X},{row.Tile.Y}) {row.DefId}");
            Assert.IsType<Placed>(result);
            Charge(clock, PlaceInteractTicks);
        }

        Assert.Equal(plankBefore - bill.Plank, CountItem(inv, bag, plankId));
        Assert.Equal(ironBefore - bill.IronIngot, CountItem(inv, bag, ironId));
        Assert.Equal(stoneBefore - bill.Stone, CountItem(inv, bag, stoneId));
        Assert.Equal(40, CountDef(registry, BeltNetwork.BuildingId));
        Assert.Equal(1, CountDef(registry, AddressSorter.BuildingId));
        Assert.Equal(4, CountDef(registry, Inserter.BuildingId));

        var belts = new BeltNetwork();
        belts.Compile(registry.All);
        var sorters = new AddressSorterNetwork();
        sorters.Compile(registry.All, belts);
        var inserters = new InserterNetwork();
        inserters.Compile(registry.All, belts);
        Assert.Single(sorters.Sorters);
        Assert.Equal(4, inserters.Inserters.Count);
        Assert.True(belts.Segments.Count >= 1);

        var machine = Assert.Single(sorters.Sorters);
        var input = SegmentEndingAt(belts, machine.Ports.Input);
        Assert.True(input.TryInsert(0, 1, InsertMetres));
        belts.StepTicks(StepTicks);
        AssertMovedOrTaken(input, machine, InsertMetres);

        Assert.True(clock.Now <= (uint)(prepTicks + deliveryTicks));
        Assert.True(clock.State.Phase is RunPhase.Prep or RunPhase.Delivery);
        Console.WriteLine(
            $"origin=({origin.X},{origin.Y}) split={split.Input}+{split.Forward}+{split.Left}+{split.Right} " +
            $"plankShop={plankGranted} ironShop={ironGranted} stoneHarvest={stoneGranted} " +
            $"clock.Now={clock.Now} phase={clock.State.Phase}");
    }

    private static (TileCoord Origin, BeltLengths Split, FactoryTile[] Layout) Sit(
        WorldTables tables,
        PlacementField field,
        bool[] occupied,
        HashSet<TileCoord> nodes)
    {
        for (int sy = 0; sy < tables.Height; sy++)
        {
            for (int sx = 0; sx < tables.Width; sx++)
            {
                var origin = new TileCoord(sx, sy);
                if (!TryCaps(origin, field, occupied, nodes, out var cap))
                    continue;
                if (!TryAllocate(cap, out var split))
                    continue;
                if (!TryLayout(origin, split, field, occupied, nodes, out var layout))
                    continue;
                if (!TryFlatten(field, layout))
                    continue;
                return (origin, split, layout);
            }
        }

        Assert.Fail("no Small Island origin fits 40 belts, sorter, and 4 inserters");
        return default;
    }

    private static bool TryCaps(
        TileCoord origin,
        PlacementField field,
        bool[] occupied,
        HashSet<TileCoord> nodes,
        out int[] cap)
    {
        cap = new int[4];
        var sorter = Covered(AddressSorter.BuildingId, origin);
        for (int i = 0; i < sorter.Length; i++)
        {
            if (!TileOpen(sorter[i], field, occupied, nodes))
                return false;
        }

        cap[0] = RunCap(new TileCoord(origin.X - 1, origin.Y), Facing.West, field, occupied, nodes, sorter);
        cap[1] = RunCap(new TileCoord(origin.X + 2, origin.Y), Facing.East, field, occupied, nodes, sorter);
        cap[2] = RunCap(new TileCoord(origin.X, origin.Y + 2), Facing.North, field, occupied, nodes, sorter);
        cap[3] = RunCap(new TileCoord(origin.X, origin.Y - 1), Facing.South, field, occupied, nodes, sorter);
        return cap[0] + cap[1] + cap[2] + cap[3] >= 40
            && cap[0] >= 1 && cap[1] >= 1 && cap[2] >= 1 && cap[3] >= 1;
    }

    private static int RunCap(
        TileCoord start,
        Facing step,
        PlacementField field,
        bool[] occupied,
        HashSet<TileCoord> nodes,
        TileCoord[] sorter)
    {
        int n = 0;
        var at = start;
        while (TileOpen(at, field, occupied, nodes) && !Contains(sorter, at))
        {
            n++;
            at = BeltNetwork.Next(at, step);
        }

        return n;
    }

    private static bool TryAllocate(int[] cap, out BeltLengths split)
    {
        var want = new[] { 10, 10, 10, 10 };
        for (int i = 0; i < 4; i++)
        {
            if (cap[i] < 1)
            {
                split = default;
                return false;
            }

            if (want[i] > cap[i])
                want[i] = cap[i];
        }

        int need = 40 - want[0] - want[1] - want[2] - want[3];
        for (int i = 0; i < 4 && need > 0; i++)
        {
            int spare = cap[i] - want[i];
            int take = spare < need ? spare : need;
            want[i] += take;
            need -= take;
        }

        split = new BeltLengths(want[0], want[1], want[2], want[3]);
        return need == 0 && split.Total == 40;
    }

    private static bool TryLayout(
        TileCoord origin,
        BeltLengths split,
        PlacementField field,
        bool[] occupied,
        HashSet<TileCoord> nodes,
        out FactoryTile[] layout)
    {
        layout = Array.Empty<FactoryTile>();
        var rows = new List<FactoryTile>(45);
        var used = new HashSet<TileCoord>();
        if (!TryAdd(rows, used, AddressSorter.BuildingId, origin, Facing.East, field, occupied, nodes))
            return false;

        var inputStart = new TileCoord(origin.X - split.Input, origin.Y);
        if (!TryAddRun(rows, used, inputStart, Facing.East, split.Input, field, occupied, nodes))
            return false;
        var forwardStart = new TileCoord(origin.X + 2, origin.Y);
        if (!TryAddRun(rows, used, forwardStart, Facing.East, split.Forward, field, occupied, nodes))
            return false;
        var leftStart = new TileCoord(origin.X, origin.Y + 2);
        if (!TryAddRun(rows, used, leftStart, Facing.North, split.Left, field, occupied, nodes))
            return false;
        var rightStart = new TileCoord(origin.X, origin.Y - 1);
        if (!TryAddRun(rows, used, rightStart, Facing.South, split.Right, field, occupied, nodes))
            return false;

        var preferred = new[]
        {
            new FactoryTile(Inserter.BuildingId, new TileCoord(origin.X - split.Input - 1, origin.Y), Facing.East),
            new FactoryTile(Inserter.BuildingId, new TileCoord(origin.X + 2, origin.Y + 1), Facing.North),
            new FactoryTile(Inserter.BuildingId, new TileCoord(origin.X + 1, origin.Y + 2), Facing.West),
            new FactoryTile(Inserter.BuildingId, new TileCoord(origin.X + 1, origin.Y - 1), Facing.West)
        };
        foreach (var row in preferred)
        {
            if (used.Contains(row.Tile)) continue;
            TryAdd(rows, used, row.DefId, row.Tile, row.Rotation, field, occupied, nodes);
        }

        if (CountDef(rows, Inserter.BuildingId) < 4
            && !TryFillInserters(rows, used, origin, field, occupied, nodes))
            return false;

        if (CountDef(rows, BeltNetwork.BuildingId) != 40)
            return false;
        if (CountDef(rows, AddressSorter.BuildingId) != 1)
            return false;
        if (CountDef(rows, Inserter.BuildingId) != 4)
            return false;

        layout = rows.ToArray();
        return true;
    }

    private static bool TryFillInserters(
        List<FactoryTile> rows,
        HashSet<TileCoord> used,
        TileCoord origin,
        PlacementField field,
        bool[] occupied,
        HashSet<TileCoord> nodes)
    {
        var candidates = new List<TileCoord>();
        foreach (var row in rows)
        {
            if (!string.Equals(row.DefId, BeltNetwork.BuildingId, StringComparison.Ordinal))
                continue;
            foreach (var neighbor in row.Tile.EdgeNeighbors())
            {
                if (used.Contains(neighbor) || candidates.Contains(neighbor)) continue;
                if (!TileOpen(neighbor, field, occupied, nodes)) continue;
                candidates.Add(neighbor);
            }
        }

        candidates.Sort((a, b) =>
        {
            int byDist = Manhattan(a, origin).CompareTo(Manhattan(b, origin));
            if (byDist != 0) return byDist;
            int byX = a.X.CompareTo(b.X);
            return byX != 0 ? byX : a.Y.CompareTo(b.Y);
        });

        foreach (var tile in candidates)
        {
            if (CountDef(rows, Inserter.BuildingId) >= 4) return true;
            var belt = AdjacentBelt(rows, tile);
            if (belt is null) continue;
            TryAdd(rows, used, Inserter.BuildingId, tile, Toward(tile, belt.Value), field, occupied, nodes);
        }

        return CountDef(rows, Inserter.BuildingId) == 4;
    }

    private static bool TryAddRun(
        List<FactoryTile> rows,
        HashSet<TileCoord> used,
        TileCoord start,
        Facing facing,
        int length,
        PlacementField field,
        bool[] occupied,
        HashSet<TileCoord> nodes)
    {
        var at = start;
        for (int i = 0; i < length; i++)
        {
            if (!TryAdd(rows, used, BeltNetwork.BuildingId, at, facing, field, occupied, nodes))
                return false;
            at = BeltNetwork.Next(at, facing);
        }

        return true;
    }

    private static bool TryAdd(
        List<FactoryTile> rows,
        HashSet<TileCoord> used,
        string defId,
        TileCoord tile,
        Facing rotation,
        PlacementField field,
        bool[] occupied,
        HashSet<TileCoord> nodes)
    {
        var covered = Covered(defId, tile);
        for (int i = 0; i < covered.Length; i++)
        {
            if (used.Contains(covered[i])) return false;
            if (!TileOpen(covered[i], field, occupied, nodes)) return false;
        }

        for (int i = 0; i < covered.Length; i++)
            used.Add(covered[i]);
        rows.Add(new FactoryTile(defId, tile, rotation));
        return true;
    }

    private static bool TryFlatten(PlacementField field, IReadOnlyList<FactoryTile> layout)
    {
        var originals = new Dictionary<(int X, int Y), int>();
        for (int i = 0; i < layout.Count; i++)
        {
            var covered = Covered(layout[i].DefId, layout[i].Tile);
            if (!field.TryPlanFlatten(covered, out var planned))
            {
                Restore(field, originals);
                return false;
            }

            for (int p = 0; p < planned.Length; p++)
            {
                var tile = new TileCoord(planned[p].X, planned[p].Y);
                var key = (tile.X, tile.Y);
                if (!originals.ContainsKey(key))
                    originals[key] = field.HeightAt(tile);
            }

            field.ApplyFlatten(planned);
        }

        Restore(field, originals);
        return true;
    }

    private static void Restore(PlacementField field, Dictionary<(int X, int Y), int> originals)
    {
        if (originals.Count == 0) return;
        var planned = new FlattenedTile[originals.Count];
        int n = 0;
        foreach (var pair in originals)
            planned[n++] = new FlattenedTile(pair.Key.X, pair.Key.Y, pair.Value);
        field.ApplyFlatten(planned);
    }

    private static bool TileOpen(
        TileCoord tile,
        PlacementField field,
        bool[] occupied,
        HashSet<TileCoord> nodes)
    {
        if (!field.InBounds(tile)) return false;
        if (field.IsWater(tile) || field.IsStreet(tile)) return false;
        if (occupied[WorldGrid.Idx(tile.X, tile.Y, field.Width)]) return false;
        return !nodes.Contains(tile);
    }

    private static TileCoord[] Covered(string defId, TileCoord origin)
    {
        if (!string.Equals(defId, AddressSorter.BuildingId, StringComparison.Ordinal))
            return new[] { origin };
        return new[]
        {
            origin,
            new TileCoord(origin.X + 1, origin.Y),
            new TileCoord(origin.X, origin.Y + 1),
            new TileCoord(origin.X + 1, origin.Y + 1)
        };
    }

    private static MaterialBill BillOf(
        IReadOnlyList<FactoryTile> layout,
        BuildingDef[] buildings,
        RecipeDef[] recipes)
    {
        var byBuilding = new Dictionary<string, BuildingDef>(StringComparer.Ordinal);
        foreach (var building in buildings)
            byBuilding[building.Id] = building;
        var byRecipe = new Dictionary<string, RecipeDef>(StringComparer.Ordinal);
        foreach (var recipe in recipes)
            byRecipe[recipe.Id] = recipe;

        int plank = 0;
        int iron = 0;
        int stone = 0;
        foreach (var row in layout)
        {
            if (!byBuilding.TryGetValue(row.DefId, out var building))
                throw new InvalidOperationException(row.DefId);
            if (!byRecipe.TryGetValue(building.Recipe, out var recipe))
                throw new InvalidOperationException(building.Recipe);
            foreach (var input in recipe.Inputs)
            {
                switch (input.Item)
                {
                    case "plank":
                        plank += input.Count;
                        break;
                    case "iron_ingot":
                        iron += input.Count;
                        break;
                    case "stone":
                        stone += input.Count;
                        break;
                    default:
                        throw new InvalidOperationException(input.Item);
                }
            }
        }

        return new MaterialBill(plank, iron, stone);
    }

    private static int Buy(ShopSession shop, ShiftClock clock, string sku, int times)
    {
        int granted = 0;
        for (int i = 0; i < times; i++)
        {
            var bought = Assert.IsType<ShopBought>(shop.TryBuy(sku));
            granted += bought.Count;
            Charge(clock, ShopBuyTicks);
        }

        return granted;
    }

    private static void Charge(ShiftClock clock, int ticks)
        => clock.AdvanceTo(clock.Now + (uint)ticks);

    private static void AssertMovedOrTaken(BeltSegment input, AddressSorter sorter, float before)
    {
        foreach (var item in input.Lane(0))
        {
            if (item.ItemId != 1) continue;
            Assert.True(item.MetresFromStart > before);
            return;
        }

        bool taken = sorter.BufferCount > 0;
        foreach (SorterOutput dest in new[]
        {
            SorterOutput.Left,
            SorterOutput.Forward,
            SorterOutput.Right,
            SorterOutput.Overflow
        })
        {
            foreach (var item in sorter.Emitted(dest))
            {
                if (item.ItemId == 1) taken = true;
            }
        }

        Assert.True(taken);
    }

    private static BeltSegment SegmentEndingAt(BeltNetwork belts, TileCoord ahead)
    {
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            if (belts.Segments[i].AheadTile.Equals(ahead))
                return belts.Segments[i];
        }

        throw new InvalidOperationException("missing input segment");
    }

    private static bool[] OccupiedMask(WorldTables tables)
    {
        var occ = new bool[tables.Width * tables.Height];
        WorldGrid.FillOccupied(occ, tables.Width, tables.Height, tables.PostOffice, tables.Streets, tables.Lots);
        return occ;
    }

    private static HashSet<TileCoord> ResourceTiles(WorldTables tables)
    {
        var nodes = new HashSet<TileCoord>();
        foreach (var node in tables.ResourceNodes)
            nodes.Add(node.Tile);
        return nodes;
    }

    private static HashSet<TileCoord> TilesOf(IReadOnlyList<FactoryTile> layout)
    {
        var tiles = new HashSet<TileCoord>();
        foreach (var row in layout)
        {
            var covered = Covered(row.DefId, row.Tile);
            for (int i = 0; i < covered.Length; i++)
                tiles.Add(covered[i]);
        }

        return tiles;
    }

    private static TileCoord FirstStone(WorldTables tables, HashSet<TileCoord> blocked)
    {
        foreach (var node in tables.ResourceNodes)
        {
            if (node.Kind != ResourceKind.Stone) continue;
            if (blocked.Contains(node.Tile)) continue;
            return node.Tile;
        }

        throw new InvalidOperationException("no free stone node");
    }

    private static TileCoord? AdjacentBelt(List<FactoryTile> rows, TileCoord tile)
    {
        foreach (var row in rows)
        {
            if (!string.Equals(row.DefId, BeltNetwork.BuildingId, StringComparison.Ordinal))
                continue;
            if (tile.SharesEdgeWith(row.Tile))
                return row.Tile;
        }

        return null;
    }

    private static Facing Toward(TileCoord from, TileCoord to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 1 && dy == 0) return Facing.East;
        if (dx == -1 && dy == 0) return Facing.West;
        if (dx == 0 && dy == 1) return Facing.North;
        if (dx == 0 && dy == -1) return Facing.South;
        throw new ArgumentOutOfRangeException(nameof(to), to, null);
    }

    private static int Manhattan(TileCoord a, TileCoord b)
        => WorldGrid.Manhattan(a.X, a.Y, b.X, b.Y);

    private static bool Contains(TileCoord[] tiles, TileCoord tile)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].Equals(tile)) return true;
        }

        return false;
    }

    private static int CountDef(ConstructRegistry registry, string defId)
    {
        int n = 0;
        foreach (var row in registry.All)
        {
            if (string.Equals(row.DefId, defId, StringComparison.Ordinal))
                n++;
        }

        return n;
    }

    private static int CountDef(List<FactoryTile> rows, string defId)
    {
        int n = 0;
        foreach (var row in rows)
        {
            if (string.Equals(row.DefId, defId, StringComparison.Ordinal))
                n++;
        }

        return n;
    }

    private static int CountItem(InventorySystem inv, ContainerId bag, ItemDefId id)
    {
        Assert.True(inv.TryGetContainer(bag, out var grid));
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

    private readonly record struct FactoryTile(string DefId, TileCoord Tile, Facing Rotation);

    private readonly record struct MaterialBill(int Plank, int IronIngot, int Stone);

    private readonly record struct BeltLengths(int Input, int Forward, int Left, int Right)
    {
        public int Total => Input + Forward + Left + Right;
    }
}
