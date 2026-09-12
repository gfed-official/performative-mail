using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.UI;

public sealed class RouteEditorTests
{
    private static readonly EntityId DepotId = EntityId.FromClassAndCounter(EntityClass.Construct, 3);

    [Fact]
    public void ClickHousesInOrder_PersistsStopOrderOnDepotRoute()
    {
        var site = Site(new TileCoord(4, 5));
        var editor = new RouteEditor(site, MapBoot.Tables());

        Assert.True(editor.TryAddHouse(MapBoot.HouseA));
        Assert.True(editor.TryAddHouse(MapBoot.HouseB));

        Assert.Equal(2, site.Route.Stops.Count);
        Assert.Equal(RouteStopKind.Address, site.Route.Stops[0].Kind);
        Assert.Equal(MapBoot.HouseA, site.Route.Stops[0].Address);
        Assert.Equal(RouteStopKind.Address, site.Route.Stops[1].Kind);
        Assert.Equal(MapBoot.HouseB, site.Route.Stops[1].Address);
        Assert.Equal(site.Route.Stops, editor.Stops);

        editor.MoveStop(0, 1);
        Assert.Equal(MapBoot.HouseB, site.Route.Stops[0].Address);
        Assert.Equal(MapBoot.HouseA, site.Route.Stops[1].Address);
        Assert.Equal(2, site.Route.Stops.Count);
    }

    [Fact]
    public void ClickTilesInOrder_WritesHouseStopsOnDepotRoute()
    {
        var site = Site(new TileCoord(4, 5));
        var editor = new RouteEditor(site, MapBoot.Tables());

        Assert.True(editor.TryClickTile(new TileCoord(0, 8)));
        Assert.True(editor.TryClickTile(new TileCoord(1, 14)));

        Assert.Equal(new[]
        {
            RouteStop.ForAddress(MapBoot.HouseA),
            RouteStop.ForAddress(MapBoot.HouseB)
        }, site.Route.Stops);
    }

    [Fact]
    public void ClickDistrictLabel_WritesMacroThatExpandsToDistrictAddresses()
    {
        var world = DebugWorld.Tables();
        var site = Site(new TileCoord(0, 0));
        var editor = new RouteEditor(site, world);

        Assert.True(editor.TryAddDistrict(1));
        Assert.Single(site.Route.Stops);
        Assert.Equal(RouteStopKind.District, site.Route.Stops[0].Kind);
        Assert.Equal((byte)1, site.Route.Stops[0].District);

        var expanded = editor.ExpandDistrict(1);
        Assert.Equal(2, expanded.Count);
        Assert.Equal(world.Houses[0].Address, expanded[0]);
        Assert.Equal(world.Houses[1].Address, expanded[1]);
        Assert.All(expanded, address => Assert.True(site.Route.Stops[0].Accepts(address)));
        Assert.False(site.Route.Stops[0].Accepts(MapBoot.HouseB));
    }

    [Fact]
    public void ClickDistrictLabelTile_WritesDistrictMacroOnDepotRoute()
    {
        var site = Site(new TileCoord(4, 5));
        var world = MapBoot.Tables();
        var editor = new RouteEditor(site, world);
        var label = MapFrame.DistrictLabelTile(world, 2);

        Assert.True(editor.TryClickTile(label));
        Assert.Equal(RouteStop.ForDistrict(2), Assert.Single(site.Route.Stops));
        Assert.True(site.Route.Stops[0].Accepts(MapBoot.HouseB));
        Assert.False(site.Route.Stops[0].Accepts(MapBoot.HouseA));
        Assert.Equal(new[] { MapBoot.HouseB }, editor.ExpandDistrict(2));
    }

    [Fact]
    public void Estimate_UsesRoutingGraphRoundTrip()
    {
        var world = ConnectedTables();
        var site = Site(new TileCoord(4, 5));
        var editor = new RouteEditor(site, world);
        Assert.Equal(new TileCoord(5, 6), site.ParkingZone);
        Assert.True(editor.TryAddHouse(Near));
        Assert.True(editor.TryAddHouse(Far));

        Assert.True(editor.TryEstimate(out int length, out int seconds));
        Assert.True(site.Route.TryRoundTrip(
            new RoutingGraph(world.RouteNodes, world.RouteEdges),
            site.ParkingZone,
            Anchors(world),
            out var trip));
        Assert.Equal(trip.LengthTiles, length);
        Assert.Equal(6 + 6, length);
        double metres = length * (world.TileCm / 100.0);
        Assert.Equal(
            (int)Math.Round(metres / VehicleContext.MailTruckOnRoadMetersPerSecond, MidpointRounding.AwayFromZero),
            seconds);
        Assert.True(seconds > 0);
    }

    [Fact]
    public void UnknownHouseOrEmptyDistrict_DoesNotWrite()
    {
        var site = Site(new TileCoord(4, 5));
        var editor = new RouteEditor(site, MapBoot.Tables());
        Assert.False(editor.TryAddHouse(new AddressId(9, 9, 9, 0)));
        Assert.False(editor.TryAddDistrict(9));
        Assert.False(editor.TryClickTile(new TileCoord(2, 2)));
        Assert.Empty(site.Route.Stops);
    }

    private static readonly AddressId Near = new(1, 1, 3, 0);
    private static readonly AddressId Far = new(1, 1, 9, 0);

    private static VehicleDepotSite Site(TileCoord origin) =>
        new(DepotId, origin, Facing.East);

    private static RouteAnchors Anchors(WorldTables world)
    {
        var anchors = new RouteAnchors();
        foreach (var house in world.Houses)
            anchors.AddAddress(house.Address, house.Mailbox.Tile(world.TileCm));
        return anchors;
    }

    private static WorldTables ConnectedTables()
    {
        int width = MapBoot.Width;
        int height = MapBoot.Height;
        int count = width * height;
        var heights = new short[count];
        var buildable = new bool[count];
        for (int i = 0; i < count; i++)
        {
            heights[i] = DebugWorld.LandHeightCm;
            buildable[i] = true;
        }

        return new WorldTables(
            width,
            height,
            MapBoot.TileCm,
            heights,
            new[]
            {
                new HouseRecord(Near, new TileCoord(4, 4), DebugWorld.LotSize, new MailboxPose(1000, 1200, 0, 0)),
                new HouseRecord(Far, new TileCoord(4, 10), DebugWorld.LotSize, new MailboxPose(1000, 2400, 0, 0)),
            },
            buildable,
            heightmapAttempts: 1,
            new PostOfficeRecord(
                DebugWorld.PostOfficeTile,
                DebugWorld.PostOfficeSize,
                DebugWorld.SpawnPadTile,
                DebugWorld.IntakeTile,
                Facing.East),
            new[] { new StreetRecord(1, "Route Row", 1, new[] { new TileCoord(5, 6), new TileCoord(5, 12) }) },
            Array.Empty<LotRecord>(),
            Array.Empty<ResourceNodeRecord>(),
            Array.Empty<FerryLaneRecord>(),
            new[] { new RouteNodeRecord(1, new TileCoord(5, 6)), new RouteNodeRecord(2, new TileCoord(5, 12)) },
            new[] { new RouteEdgeRecord(1, 2, 6, 0) },
            Array.Empty<SpawnEdgeRecord>(),
            validationAttempts: 1);
    }
}
