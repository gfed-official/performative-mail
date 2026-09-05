using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class BeltMk1Tests
{
    private static readonly TileCoord Origin = new(1, 1);
    private static readonly TileCoord JunctionOrigin = new(3, 2);
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    [Fact]
    public void RepoDefs_BeltMk1_MatchesChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue(BeltNetwork.BuildingId, out var belt));
        Assert.True(recipes.TryGetValue(belt.Recipe, out var recipe));
        Assert.Equal("recipe_belt_mk1", belt.Recipe);
        Assert.Equal(BeltNetwork.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(80, belt.Hp);
        Assert.Equal(1, belt.Footprint.W);
        Assert.Equal(1, belt.Footprint.H);
        Assert.False(belt.OnStreet);
        Assert.Equal(WaterPlacement.None, belt.OnWater);
        Assert.Equal(15, belt.MaxSlopeDeg);
        Assert.Equal(BuildingBehaviour.Belt, belt.Behaviour);
        Assert.Null(belt.Container);
        Assert.Null(recipe.Blueprint);
        Assert.Equal("plank", recipe.Inputs[0].Item);
        Assert.Equal(1, recipe.Inputs[0].Count);
        Assert.Equal("iron_ingot", recipe.Inputs[1].Item);
        Assert.Equal(1, recipe.Inputs[1].Count);
    }

    [Fact]
    public void RepoDefs_RampAndElevated_MatchChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue(BeltNetwork.BuildingId, out var flat));
        Assert.False(flat.OnStreet);

        Assert.True(buildings.TryGetValue(BeltNetwork.RampId, out var ramp));
        Assert.True(recipes.TryGetValue(ramp.Recipe, out var rampRecipe));
        Assert.Equal("recipe_belt_mk1_ramp", ramp.Recipe);
        Assert.Equal(BeltNetwork.RampId, rampRecipe.ProducesBuilding);
        Assert.Equal(120, ramp.Hp);
        Assert.Equal(2, ramp.Footprint.W);
        Assert.Equal(1, ramp.Footprint.H);
        Assert.Equal(4, ramp.Rotations);
        Assert.False(ramp.OnStreet);
        Assert.Equal(WaterPlacement.None, ramp.OnWater);
        Assert.Equal(15, ramp.MaxSlopeDeg);
        Assert.Equal(BuildingBehaviour.Belt, ramp.Behaviour);
        Assert.Null(ramp.Container);
        Assert.Null(rampRecipe.Blueprint);
        Assert.Equal("plank", rampRecipe.Inputs[0].Item);
        Assert.Equal(3, rampRecipe.Inputs[0].Count);
        Assert.Equal("iron_ingot", rampRecipe.Inputs[1].Item);
        Assert.Equal(2, rampRecipe.Inputs[1].Count);

        Assert.True(buildings.TryGetValue(BeltNetwork.ElevatedId, out var elevated));
        Assert.True(recipes.TryGetValue(elevated.Recipe, out var elevatedRecipe));
        Assert.Equal("recipe_belt_mk1_elevated", elevated.Recipe);
        Assert.Equal(BeltNetwork.ElevatedId, elevatedRecipe.ProducesBuilding);
        Assert.Equal(80, elevated.Hp);
        Assert.Equal(1, elevated.Footprint.W);
        Assert.Equal(1, elevated.Footprint.H);
        Assert.True(elevated.OnStreet);
        Assert.Equal(WaterPlacement.None, elevated.OnWater);
        Assert.Equal(15, elevated.MaxSlopeDeg);
        Assert.Equal(BuildingBehaviour.Belt, elevated.Behaviour);
        Assert.Null(elevated.Container);
        Assert.Null(elevatedRecipe.Blueprint);
        Assert.Equal("plank", elevatedRecipe.Inputs[0].Item);
        Assert.Equal(2, elevatedRecipe.Inputs[0].Count);
        Assert.Equal("iron_ingot", elevatedRecipe.Inputs[1].Item);
        Assert.Equal(1, elevatedRecipe.Inputs[1].Count);
    }

    [Fact]
    public void Place_FourTiles_ConsumesOnePlankAndOneIngotEach()
    {
        var fx = Loaded(planks: 4, iron: 4);

        for (int x = 0; x < 4; x++)
            Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1 + x, 1), Facing.East, Owner));

        Assert.Equal(4, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, PlankId));
        Assert.Equal(0, CountItem(fx, IronId));
    }

    [Fact]
    public void Street_Rejected_DoesNotConsume()
    {
        var field = PlacementField.Flat(8, 6, 200).WithStreet(Origin);
        var fx = Loaded(planks: 1, iron: 1, field);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));

        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx, PlankId));
        Assert.Equal(1, CountItem(fx, IronId));
    }

    [Fact]
    public void Street_ElevatedPlacesAndConsumes_FlatRejectedDoesNotConsume()
    {
        var field = PlacementField.Flat(8, 6, 200).WithStreet(Origin);
        var fx = Loaded(planks: 2, iron: 1, field);

        var flat = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));
        Assert.Equal(PlaceReject.Street, flat.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(2, CountItem(fx, PlankId));
        Assert.Equal(1, CountItem(fx, IronId));

        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.ElevatedId, Origin, Facing.East));
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, PlankId));
        Assert.Equal(0, CountItem(fx, IronId));
    }

    [Fact]
    public void Ramp_East_OccupiesOriginAndNext_SecondPlaceOccupied()
    {
        var fx = Loaded(planks: 6, iron: 4);
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.RampId, Origin, Facing.East));

        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(3, CountItem(fx, PlankId));
        Assert.Equal(2, CountItem(fx, IronId));

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.RampId, new TileCoord(2, 1), Facing.East));
        Assert.Equal(PlaceReject.Occupied, rejected.Reason);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(3, CountItem(fx, PlankId));
        Assert.Equal(2, CountItem(fx, IronId));
    }

    [Fact]
    public void Ramp_WestAndSouth_OccupyAlongFacing()
    {
        var west = Loaded(planks: 6, iron: 4);
        Assert.IsType<Placed>(west.Registry.TryPlace(BeltNetwork.RampId, new TileCoord(3, 2), Facing.West));
        var westHit = Assert.IsType<PlaceRejected>(
            west.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(2, 2), Facing.West));
        Assert.Equal(PlaceReject.Occupied, westHit.Reason);
        Assert.Equal(1, west.Registry.Count);
        Assert.Equal(3, CountItem(west, PlankId));
        Assert.Equal(2, CountItem(west, IronId));

        var south = Loaded(planks: 6, iron: 4);
        Assert.IsType<Placed>(south.Registry.TryPlace(BeltNetwork.RampId, new TileCoord(2, 3), Facing.South));
        var southHit = Assert.IsType<PlaceRejected>(
            south.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(2, 2), Facing.South));
        Assert.Equal(PlaceReject.Occupied, southHit.Reason);
        Assert.Equal(1, south.Registry.Count);
        Assert.Equal(3, CountItem(south, PlankId));
        Assert.Equal(2, CountItem(south, IronId));
    }

    [Fact]
    public void Ramp_StreetOnAnyCoveredTile_Rejected()
    {
        var onOrigin = Loaded(planks: 3, iron: 2, PlacementField.Flat(8, 6, 200).WithStreet(Origin));
        var originHit = Assert.IsType<PlaceRejected>(
            onOrigin.Registry.TryPlace(BeltNetwork.RampId, Origin, Facing.East));
        Assert.Equal(PlaceReject.Street, originHit.Reason);
        Assert.Equal(0, onOrigin.Registry.Count);
        Assert.Equal(3, CountItem(onOrigin, PlankId));
        Assert.Equal(2, CountItem(onOrigin, IronId));

        var onNext = Loaded(planks: 3, iron: 2, PlacementField.Flat(8, 6, 200).WithStreet(new TileCoord(2, 1)));
        var nextHit = Assert.IsType<PlaceRejected>(
            onNext.Registry.TryPlace(BeltNetwork.RampId, Origin, Facing.East));
        Assert.Equal(PlaceReject.Street, nextHit.Reason);
        Assert.Equal(0, onNext.Registry.Count);
        Assert.Equal(3, CountItem(onNext, PlankId));
        Assert.Equal(2, CountItem(onNext, IronId));
    }

    [Fact]
    public void Water_Rejected_DoesNotConsume()
    {
        var field = PlacementField.Flat(8, 6, 200).WithHeight(Origin, 0);
        var fx = Loaded(planks: 1, iron: 1, field);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));

        Assert.Equal(PlaceReject.Water, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx, PlankId));
        Assert.Equal(1, CountItem(fx, IronId));
    }

    [Fact]
    public void Occupied_Rejected_DoesNotConsume()
    {
        var fx = Loaded(planks: 2, iron: 2);
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.North));

        Assert.Equal(PlaceReject.Occupied, rejected.Reason);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx, PlankId));
        Assert.Equal(1, CountItem(fx, IronId));
    }

    [Fact]
    public void MissingInput_Rejected()
    {
        var fx = Loaded(planks: 1, iron: 0);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));

        Assert.Equal(PlaceReject.MissingInput, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx, PlankId));
    }

    [Fact]
    public void Compile_FourEastTiles_OneEightMetreSegment()
    {
        var fx = PlaceEastRun(4);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(Facing.East, segment.Facing);
        Assert.Equal(8f, segment.LengthMetres);
        Assert.Equal(4, segment.Tiles.Count);
        Assert.Equal(new TileCoord(1, 1), segment.Tiles[0]);
        Assert.Equal(new TileCoord(4, 1), segment.Tiles[3]);
        Assert.Equal(BeltNetwork.LaneCount, 2);
        Assert.Empty(segment.Lane(0));
        Assert.Empty(segment.Lane(1));
    }

    [Fact]
    public void Compile_TwoPlusAdjacentSameFacing_IsOneSegmentNotPerTile()
    {
        var fx = PlaceEastRun(2);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(4f, segment.LengthMetres);
        Assert.Equal(2, segment.Tiles.Count);
    }

    [Fact]
    public void Compile_ThreeTileLine_DifferentLengthAndHash()
    {
        var four = CompileEast(4);
        var three = CompileEast(3);

        Assert.Equal(8f, four.LengthMetres);
        Assert.Equal(6f, three.LengthMetres);
        Assert.NotEqual(four.RunHash, three.RunHash);
    }

    [Fact]
    public void Compile_SameRunTwice_HashesMatch()
    {
        var fx = PlaceEastRun(4);
        var first = new BeltNetwork();
        first.Compile(fx.Registry.All);
        var second = new BeltNetwork();
        second.Compile(fx.Registry.All);

        Assert.Equal(first.Segments[0].RunHash, second.Segments[0].RunHash);
        Assert.Equal(first.Segments[0].LengthMetres, second.Segments[0].LengthMetres);
        Assert.Equal(first.Segments[0].Tiles, second.Segments[0].Tiles);
    }

    [Fact]
    public void Compile_GapOrOppositeFacing_IsTwoSegments()
    {
        var gap = Loaded(planks: 2, iron: 2);
        Assert.IsType<Placed>(gap.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1, 1), Facing.East));
        Assert.IsType<Placed>(gap.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(3, 1), Facing.East));
        var gapBelts = new BeltNetwork();
        gapBelts.Compile(gap.Registry.All);
        Assert.Equal(2, gapBelts.Segments.Count);

        var opposed = Loaded(planks: 2, iron: 2);
        Assert.IsType<Placed>(opposed.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1, 1), Facing.East));
        Assert.IsType<Placed>(opposed.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(2, 1), Facing.West));
        var opposedBelts = new BeltNetwork();
        opposedBelts.Compile(opposed.Registry.All);
        Assert.Equal(2, opposedBelts.Segments.Count);
    }

    [Fact]
    public void Compile_LCornerEastThenNorth_OneSixMetreSegment()
    {
        var fx = Place(
            (new TileCoord(1, 1), Facing.East),
            (new TileCoord(2, 1), Facing.North),
            (new TileCoord(2, 2), Facing.North));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(Facing.East, segment.Facing);
        Assert.Equal(6f, segment.LengthMetres);
        Assert.Equal(3, segment.Tiles.Count);
        Assert.Equal(new TileCoord(1, 1), segment.Tiles[0]);
        Assert.Equal(new TileCoord(2, 1), segment.Tiles[1]);
        Assert.Equal(new TileCoord(2, 2), segment.Tiles[2]);
    }

    [Fact]
    public void Compile_FacingChange_IsNotOneNodePerTile()
    {
        var fx = Place(
            (new TileCoord(1, 1), Facing.East),
            (new TileCoord(2, 1), Facing.South));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(2, segment.Tiles.Count);
        Assert.Equal(4f, segment.LengthMetres);
        Assert.Equal(new TileCoord(1, 1), segment.Tiles[0]);
        Assert.Equal(new TileCoord(2, 1), segment.Tiles[1]);
    }

    [Fact]
    public void Compile_AdjacentSameTierJoinAtFacingChange_OnePath()
    {
        var fx = Place(
            (new TileCoord(1, 1), Facing.East),
            (new TileCoord(2, 1), Facing.East),
            (new TileCoord(3, 1), Facing.North),
            (new TileCoord(3, 2), Facing.North));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(Facing.East, segment.Facing);
        Assert.Equal(8f, segment.LengthMetres);
        Assert.Equal(4, segment.Tiles.Count);
        Assert.Equal(new TileCoord(1, 1), segment.Tiles[0]);
        Assert.Equal(new TileCoord(3, 1), segment.Tiles[2]);
        Assert.Equal(new TileCoord(3, 2), segment.Tiles[3]);
    }

    [Fact]
    public void Compile_AdjacentButNotFeeding_IsTwoSegments()
    {
        var fx = Place(
            (new TileCoord(1, 1), Facing.North),
            (new TileCoord(2, 1), Facing.East));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        Assert.Equal(2, belts.Segments.Count);
    }

    [Fact]
    public void Compile_BentRunTwice_HashesMatch()
    {
        var fx = Place(
            (new TileCoord(1, 1), Facing.East),
            (new TileCoord(2, 1), Facing.North),
            (new TileCoord(2, 2), Facing.West));
        var first = new BeltNetwork();
        first.Compile(fx.Registry.All);
        var second = new BeltNetwork();
        second.Compile(fx.Registry.All);

        var a = Assert.Single(first.Segments);
        var b = Assert.Single(second.Segments);
        Assert.Equal(a.RunHash, b.RunHash);
        Assert.Equal(a.LengthMetres, b.LengthMetres);
        Assert.Equal(a.Tiles, b.Tiles);
    }

    [Fact]
    public void Compile_LoneEastRamp_OneFourMetreTwoTileSegment()
    {
        var fx = Loaded(planks: 3, iron: 2);
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.RampId, Origin, Facing.East));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(Facing.East, segment.Facing);
        Assert.Equal(4f, segment.LengthMetres);
        Assert.Equal(2, segment.Tiles.Count);
        Assert.Equal(new TileCoord(1, 1), segment.Tiles[0]);
        Assert.Equal(new TileCoord(2, 1), segment.Tiles[1]);
    }

    [Fact]
    public void Compile_FlatThenRampThenElevated_OnePath()
    {
        var fx = Loaded(planks: 6, iron: 4);
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1, 1), Facing.East));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.RampId, new TileCoord(2, 1), Facing.East));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.ElevatedId, new TileCoord(4, 1), Facing.East));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(Facing.East, segment.Facing);
        Assert.Equal(8f, segment.LengthMetres);
        Assert.Equal(4, segment.Tiles.Count);
        Assert.Equal(new TileCoord(1, 1), segment.Tiles[0]);
        Assert.Equal(new TileCoord(2, 1), segment.Tiles[1]);
        Assert.Equal(new TileCoord(3, 1), segment.Tiles[2]);
        Assert.Equal(new TileCoord(4, 1), segment.Tiles[3]);
    }

    [Fact]
    public void Occupancy_CompiledRampTiles_AreOccupied()
    {
        AssertRampOccupancyMatchesCompile(new TileCoord(1, 1), Facing.East, new TileCoord(2, 1));
        AssertRampOccupancyMatchesCompile(new TileCoord(3, 2), Facing.West, new TileCoord(2, 2));
        AssertRampOccupancyMatchesCompile(new TileCoord(2, 3), Facing.South, new TileCoord(2, 2));
    }

    [Fact]
    public void Step_FlatRampElevated_ThirtyTicks_AtTwoMetres()
    {
        var fx = Loaded(planks: 6, iron: 4);
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1, 1), Facing.East));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.RampId, new TileCoord(2, 1), Facing.East));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.ElevatedId, new TileCoord(4, 1), Facing.East));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 11, 0f));

        belts.StepTicks(TickClock.TickHz);

        var item = Assert.Single(segment.Lane(0));
        Assert.Equal(11, item.ItemId);
        Assert.Equal(2f, item.MetresFromStart, 3);
        Assert.Empty(segment.Lane(1));
    }

    [Fact]
    public void Step_LCorner_Lane0_ThirtyTicks_AtTwoMetres()
    {
        var fx = Place(
            (new TileCoord(1, 1), Facing.East),
            (new TileCoord(2, 1), Facing.North),
            (new TileCoord(2, 2), Facing.North));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 11, 0f));

        belts.StepTicks(TickClock.TickHz);

        var item = Assert.Single(segment.Lane(0));
        Assert.Equal(11, item.ItemId);
        Assert.Equal(2f, item.MetresFromStart, 3);
        Assert.Empty(segment.Lane(1));
    }

    [Fact]
    public void Step_Lane0_ThirtyTicks_AtTwoMetres_Lane1Empty()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 11, 0f));

        belts.StepTicks(TickClock.TickHz);

        var item = Assert.Single(segment.Lane(0));
        Assert.Equal(11, item.ItemId);
        Assert.Equal(ExpectedMetres(0f, TickClock.TickHz), item.MetresFromStart, 3);
        Assert.Equal(2f, item.MetresFromStart, 3);
        Assert.Empty(segment.Lane(1));
    }

    [Fact]
    public void Step_Lane1_ThirtyTicks_AtTwoMetres_Lane0Empty()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(1, 22, 0f));

        belts.StepTicks(TickClock.TickHz);

        var item = Assert.Single(segment.Lane(1));
        Assert.Equal(22, item.ItemId);
        Assert.Equal(ExpectedMetres(0f, TickClock.TickHz), item.MetresFromStart, 3);
        Assert.Equal(2f, item.MetresFromStart, 3);
        Assert.Empty(segment.Lane(0));
    }

    [Fact]
    public void Step_BothLanes_MoveIndependently()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 1, 0f));
        Assert.True(segment.TryInsert(1, 2, 1f));

        belts.StepTicks(TickClock.TickHz);

        Assert.Equal(ExpectedMetres(0f, TickClock.TickHz), Assert.Single(segment.Lane(0)).MetresFromStart, 3);
        Assert.Equal(ExpectedMetres(1f, TickClock.TickHz), Assert.Single(segment.Lane(1)).MetresFromStart, 3);
    }

    [Fact]
    public void Insert_CloserThanHalfMetreBehindHead_Rejected()
    {
        var segment = CompileEast(4);
        Assert.True(segment.TryInsert(0, 1, 0f));
        Assert.False(segment.TryInsert(0, 2, 0.4f));
        Assert.True(segment.TryInsert(0, 3, 0.5f));
        Assert.Equal(2, segment.Lane(0).Count);
    }

    [Fact]
    public void Step_BlockedHead_DoesNotOverlap()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 1, segment.LengthMetres));
        Assert.True(segment.TryInsert(0, 2, 7.2f));

        belts.StepTicks(TickClock.TickHz);

        Assert.Equal(8f, segment.Lane(0)[0].MetresFromStart, 3);
        float follower = segment.Lane(0)[1].MetresFromStart;
        float headGap = segment.Lane(0)[0].MetresFromStart - BeltNetwork.MinSpacingMetres;
        Assert.Equal(Math.Min(ExpectedMetres(7.2f, TickClock.TickHz), headGap), follower, 3);
        Assert.True(segment.Lane(0)[0].MetresFromStart - follower >= BeltNetwork.MinSpacingMetres);
    }

    [Fact]
    public void Step_BlockedHead_BothLanesJamWithoutOverlap()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 1, 8f));
        Assert.True(segment.TryInsert(0, 2, 7.2f));
        Assert.True(segment.TryInsert(1, 3, 8f));
        Assert.True(segment.TryInsert(1, 4, 7.2f));

        belts.StepTicks(60);

        for (int lane = 0; lane < BeltNetwork.LaneCount; lane++)
        {
            Assert.Equal(8f, segment.Lane(lane)[0].MetresFromStart, 3);
            Assert.Equal(7.5f, segment.Lane(lane)[1].MetresFromStart, 3);
        }
    }

    [Fact]
    public void RepoDefs_SplitterAndMerger_MatchChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue(BeltNetwork.SplitterId, out var splitter));
        Assert.True(recipes.TryGetValue(splitter.Recipe, out var splitterRecipe));
        Assert.Equal("recipe_splitter", splitter.Recipe);
        Assert.Equal(BeltNetwork.SplitterId, splitterRecipe.ProducesBuilding);
        Assert.Equal(150, splitter.Hp);
        Assert.Equal(1, splitter.Footprint.W);
        Assert.Equal(1, splitter.Footprint.H);
        Assert.False(splitter.OnStreet);
        Assert.Equal(WaterPlacement.None, splitter.OnWater);
        Assert.Equal(15, splitter.MaxSlopeDeg);
        Assert.Equal(BuildingBehaviour.Splitter, splitter.Behaviour);
        Assert.Null(splitterRecipe.Blueprint);
        Assert.Equal("iron_ingot", splitterRecipe.Inputs[0].Item);
        Assert.Equal(2, splitterRecipe.Inputs[0].Count);
        Assert.Equal("plank", splitterRecipe.Inputs[1].Item);
        Assert.Equal(1, splitterRecipe.Inputs[1].Count);

        Assert.True(buildings.TryGetValue(BeltNetwork.MergerId, out var merger));
        Assert.True(recipes.TryGetValue(merger.Recipe, out var mergerRecipe));
        Assert.Equal("recipe_merger", merger.Recipe);
        Assert.Equal(BeltNetwork.MergerId, mergerRecipe.ProducesBuilding);
        Assert.Equal(150, merger.Hp);
        Assert.Equal(1, merger.Footprint.W);
        Assert.Equal(1, merger.Footprint.H);
        Assert.False(merger.OnStreet);
        Assert.Equal(WaterPlacement.None, merger.OnWater);
        Assert.Equal(15, merger.MaxSlopeDeg);
        Assert.Equal(BuildingBehaviour.Merger, merger.Behaviour);
        Assert.Null(mergerRecipe.Blueprint);
        Assert.Equal("iron_ingot", mergerRecipe.Inputs[0].Item);
        Assert.Equal(2, mergerRecipe.Inputs[0].Count);
        Assert.Equal("plank", mergerRecipe.Inputs[1].Item);
        Assert.Equal(1, mergerRecipe.Inputs[1].Count);
    }

    [Fact]
    public void Place_Splitter_ConsumesAndStreetRejects()
    {
        PlaceJunctionConsumesAndStreetRejects(BeltNetwork.SplitterId);
    }

    [Fact]
    public void Place_Merger_ConsumesAndStreetRejects()
    {
        PlaceJunctionConsumesAndStreetRejects(BeltNetwork.MergerId);
    }

    [Fact]
    public void Compile_BeltSplitterBelt_TwoSegmentsAndOneJunction()
    {
        var fx = Loaded(planks: 3, iron: 4);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(2, 2), Facing.East);
        PlaceId(fx, BeltNetwork.SplitterId, JunctionOrigin, Facing.East);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(4, 2), Facing.East);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        Assert.Equal(2, belts.Segments.Count);
        AssertNoSegmentContains(belts, JunctionOrigin);
        var junction = Assert.Single(belts.Junctions);
        Assert.True(junction.IsSplitter);
        Assert.Equal(JunctionOrigin, junction.Tile);
        Assert.Equal(Facing.East, junction.Facing);
        var input = Assert.Single(junction.Inputs);
        Assert.Equal(new TileCoord(2, 2), input.Tile);
        Assert.Equal(Facing.East, input.Facing);
        var output = Assert.Single(junction.Outputs);
        Assert.Equal(new TileCoord(4, 2), output.Tile);
        Assert.Equal(Facing.East, output.Facing);
    }

    [Fact]
    public void Splitter_RoundRobin_ForwardLeftRight()
    {
        var belts = CompileSplitterStar();
        var input = SegmentOn(belts, new TileCoord(2, 2));
        Assert.True(input.TryInsert(0, 1, 2.0f));
        Assert.True(input.TryInsert(0, 2, 1.5f));
        Assert.True(input.TryInsert(0, 3, 1.0f));

        belts.StepTicks(TickClock.TickHz);

        Assert.Empty(input.Lane(0));
        Assert.Equal(1, Assert.Single(SegmentOn(belts, new TileCoord(4, 2)).Lane(0)).ItemId);
        Assert.Equal(2, Assert.Single(SegmentOn(belts, new TileCoord(3, 3)).Lane(0)).ItemId);
        Assert.Equal(3, Assert.Single(SegmentOn(belts, new TileCoord(3, 1)).Lane(0)).ItemId);
    }

    [Fact]
    public void Splitter_SkipBlockedForward()
    {
        var belts = CompileSplitterStar();
        var input = SegmentOn(belts, new TileCoord(2, 2));
        var forward = SegmentOn(belts, new TileCoord(4, 2));
        Assert.True(forward.TryInsert(0, 99, 0f));
        Assert.True(input.TryInsert(0, 1, 2.0f));
        Assert.True(input.TryInsert(0, 2, 1.5f));
        Assert.True(input.TryInsert(0, 3, 1.0f));

        belts.StepTicks(TickClock.TickHz);

        Assert.Empty(input.Lane(0));
        Assert.Equal(1, Assert.Single(SegmentOn(belts, new TileCoord(3, 3)).Lane(0)).ItemId);
        Assert.Equal(2, Assert.Single(SegmentOn(belts, new TileCoord(3, 1)).Lane(0)).ItemId);
        Assert.Contains(forward.Lane(0), item => item.ItemId == 99);
        Assert.DoesNotContain(forward.Lane(0), item => item.ItemId == 1);
        Assert.DoesNotContain(forward.Lane(0), item => item.ItemId == 2);
    }

    [Fact]
    public void Splitter_KindFilter_SkipsEastForLetter()
    {
        var belts = CompileSplitterStar();
        var splitterTile = JunctionOrigin;
        Assert.True(belts.SetOutputFilter(splitterTile, Facing.East, MailKinds.Postcard));
        Assert.False(belts.SetOutputFilter(new TileCoord(0, 0), Facing.East, MailKinds.Postcard));
        var input = SegmentOn(belts, new TileCoord(2, 2));
        Assert.True(input.TryInsert(0, 7, 2.0f));

        belts.StepTicks(1);

        Assert.Empty(input.Lane(0));
        Assert.Empty(SegmentOn(belts, new TileCoord(4, 2)).Lane(0));
        Assert.Equal(7, Assert.Single(SegmentOn(belts, new TileCoord(3, 3)).Lane(0)).ItemId);
        Assert.Empty(SegmentOn(belts, new TileCoord(3, 1)).Lane(0));
    }

    [Fact]
    public void Merger_RoundRobin_DoesNotStarve()
    {
        var belts = CompileMergerStar();
        AssertNoSegmentContains(belts, JunctionOrigin);
        var west = SegmentOn(belts, new TileCoord(2, 2));
        var north = SegmentOn(belts, new TileCoord(3, 3));
        var south = SegmentOn(belts, new TileCoord(3, 1));
        var output = SegmentOn(belts, new TileCoord(4, 2));
        Assert.True(west.TryInsert(0, 10, 2.0f));
        Assert.True(west.TryInsert(0, 11, 1.5f));
        Assert.True(west.TryInsert(0, 12, 1.0f));
        Assert.True(north.TryInsert(0, 20, 2.0f));
        Assert.True(south.TryInsert(0, 30, 2.0f));

        belts.StepTicks(20);

        var ids = new HashSet<int>();
        foreach (var item in output.Lane(0))
            ids.Add(item.ItemId);
        Assert.Contains(20, ids);
        Assert.Contains(10, ids);
        Assert.Contains(30, ids);
        Assert.Equal(3, ids.Count);
        Assert.DoesNotContain(11, ids);
        Assert.DoesNotContain(12, ids);
    }

    [Fact]
    public void Compile_Merger_TileIsNotInAnySegment()
    {
        var belts = CompileMergerStar();
        var junction = Assert.Single(belts.Junctions);
        Assert.False(junction.IsSplitter);
        Assert.Equal(JunctionOrigin, junction.Tile);
        Assert.Equal(Facing.East, junction.Facing);
        AssertNoSegmentContains(belts, JunctionOrigin);
        Assert.Equal(3, junction.Inputs.Count);
        Assert.Equal(new TileCoord(3, 3), junction.Inputs[0].Tile);
        Assert.Equal(Facing.South, junction.Inputs[0].Facing);
        Assert.Equal(new TileCoord(2, 2), junction.Inputs[1].Tile);
        Assert.Equal(Facing.East, junction.Inputs[1].Facing);
        Assert.Equal(new TileCoord(3, 1), junction.Inputs[2].Tile);
        Assert.Equal(Facing.North, junction.Inputs[2].Facing);
        var output = Assert.Single(junction.Outputs);
        Assert.Equal(new TileCoord(4, 2), output.Tile);
        Assert.Equal(Facing.East, output.Facing);
    }

    private static void PlaceJunctionConsumesAndStreetRejects(string id)
    {
        var fx = Loaded(planks: 1, iron: 2);
        PlaceId(fx, id, JunctionOrigin, Facing.East);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, PlankId));
        Assert.Equal(0, CountItem(fx, IronId));

        var street = PlacementField.Flat(8, 6, 200).WithStreet(JunctionOrigin);
        var blocked = Loaded(planks: 1, iron: 2, street);
        var rejected = Assert.IsType<PlaceRejected>(
            blocked.Registry.TryPlace(id, JunctionOrigin, Facing.East));
        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(0, blocked.Registry.Count);
        Assert.Equal(1, CountItem(blocked, PlankId));
        Assert.Equal(2, CountItem(blocked, IronId));
    }

    private static BeltNetwork CompileSplitterStar()
    {
        var fx = Loaded(planks: 5, iron: 6);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(2, 2), Facing.East);
        PlaceId(fx, BeltNetwork.SplitterId, JunctionOrigin, Facing.East);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(4, 2), Facing.East);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(3, 3), Facing.North);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(3, 1), Facing.South);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        return belts;
    }

    private static BeltNetwork CompileMergerStar()
    {
        var fx = Loaded(planks: 5, iron: 6);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(2, 2), Facing.East);
        PlaceId(fx, BeltNetwork.MergerId, JunctionOrigin, Facing.East);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(4, 2), Facing.East);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(3, 3), Facing.South);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(3, 1), Facing.North);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        return belts;
    }

    private static void PlaceId(Fixture fx, string id, TileCoord tile, Facing facing)
    {
        Assert.IsType<Placed>(fx.Registry.TryPlace(id, tile, facing, Owner));
    }

    private static BeltSegment SegmentOn(BeltNetwork belts, TileCoord tile)
    {
        foreach (var segment in belts.Segments)
        {
            for (int i = 0; i < segment.Tiles.Count; i++)
            {
                if (segment.Tiles[i].Equals(tile))
                    return segment;
            }
        }

        throw new InvalidOperationException($"No segment covers {tile.X},{tile.Y}.");
    }

    private static void AssertNoSegmentContains(BeltNetwork belts, TileCoord tile)
    {
        foreach (var segment in belts.Segments)
            Assert.DoesNotContain(tile, segment.Tiles);
    }

    private static BeltNetwork CompileEastNetwork(int tiles)
    {
        var fx = PlaceEastRun(tiles);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        return belts;
    }

    private static BeltSegment CompileEast(int tiles) =>
        Assert.Single(CompileEastNetwork(tiles).Segments);

    private static void AssertRampOccupancyMatchesCompile(TileCoord origin, Facing facing, TileCoord next)
    {
        var fx = Loaded(planks: 4, iron: 3);
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.RampId, origin, facing));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(2, segment.Tiles.Count);
        Assert.Equal(origin, segment.Tiles[0]);
        Assert.Equal(next, segment.Tiles[1]);
        for (int i = 0; i < segment.Tiles.Count; i++)
        {
            var rejected = Assert.IsType<PlaceRejected>(
                fx.Registry.TryPlace(BeltNetwork.BuildingId, segment.Tiles[i], facing));
            Assert.Equal(PlaceReject.Occupied, rejected.Reason);
        }

        Assert.Equal(1, CountItem(fx, PlankId));
        Assert.Equal(1, CountItem(fx, IronId));
    }

    private static Fixture PlaceEastRun(int tiles)
    {
        var fx = Loaded(planks: tiles, iron: tiles);
        for (int i = 0; i < tiles; i++)
            Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1 + i, 1), Facing.East, Owner));
        return fx;
    }

    private static Fixture Place(params (TileCoord Tile, Facing Facing)[] belts)
    {
        var fx = Loaded(planks: belts.Length, iron: belts.Length);
        for (int i = 0; i < belts.Length; i++)
            Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, belts[i].Tile, belts[i].Facing, Owner));
        return fx;
    }

    private static float ExpectedMetres(float start, int ticks)
    {
        float t = ticks / (float)TickClock.TickHz;
        return start + BeltNetwork.Mk1MetresPerSecond * t;
    }

    private static Fixture Loaded(int planks, int iron, PlacementField? field = null)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (planks > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, planks))));
        if (iron > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, iron))));
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field ?? PlacementField.Flat(8, 6, 200),
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
        ["log"] = LogId,
        ["plank"] = PlankId,
        ["iron_ingot"] = IronId
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
            if (key.Def == LogId.Value) return new Footprint(1, 2);
            if (key.Def == PlankId.Value || key.Def == IronId.Value) return new Footprint(1, 1);
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            if (key.Def == LogId.Value) return 10;
            if (key.Def == PlankId.Value || key.Def == IronId.Value) return 20;
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return WeightClass.Light;
        }

        public StackCategory CategoryOf(StackKey key) => StackCategory.Material;
    }
}
