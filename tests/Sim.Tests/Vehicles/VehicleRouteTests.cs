using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Tests.World;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class VehicleRouteTests
{
    private static readonly AddressId Oak = new(1, 4, 13, 0);
    private static readonly EntityId Port = EntityId.FromClassAndCounter(EntityClass.Construct, 17);

    [Fact]
    public void ReplaceStops_PreservesCallerOrder()
    {
        var route = new VehicleRoute();
        var stops = new[]
        {
            RouteStop.ForAddress(Oak),
            RouteStop.ForDistrict(2),
            RouteStop.ForConstruct(Port)
        };
        route.ReplaceStops(stops);

        Assert.Equal(RouteStopKind.Address, route.Stops[0].Kind);
        Assert.Equal(Oak, route.Stops[0].Address);
        Assert.Equal(RouteStopKind.District, route.Stops[1].Kind);
        Assert.Equal((byte)2, route.Stops[1].District);
        Assert.Equal(RouteStopKind.Construct, route.Stops[2].Kind);
        Assert.Equal(Port, route.Stops[2].Construct);
        Assert.Equal(stops, route.Stops);
    }

    [Fact]
    public void RoundTrip_VisitsStopsInListOrder_NotNearestFirst()
    {
        var graph = LineGraph();
        var anchors = new RouteAnchors();
        anchors.AddAddress(Oak, new TileCoord(10, 0));
        anchors.AddDistrict(2, new TileCoord(2, 0));
        var route = new VehicleRoute();
        route.ReplaceStops(new[]
        {
            RouteStop.ForAddress(Oak),
            RouteStop.ForDistrict(2)
        });

        Assert.True(route.TryRoundTrip(graph, new TileCoord(0, 0), anchors, out var farFirst));
        Assert.Equal(new[] { 0, 2, 1, 0 }, farFirst.HopNodes);
        Assert.Equal(new[]
        {
            new TileCoord(0, 0),
            new TileCoord(10, 0),
            new TileCoord(2, 0),
            new TileCoord(0, 0)
        }, farFirst.Waypoints);
        Assert.Equal(10 + 8 + 2, farFirst.LengthTiles);
        AssertHopsAppearInOrder(farFirst);

        route.ReplaceStops(new[]
        {
            RouteStop.ForDistrict(2),
            RouteStop.ForAddress(Oak)
        });
        Assert.True(route.TryRoundTrip(graph, new TileCoord(0, 0), anchors, out var nearFirst));
        Assert.Equal(new[] { 0, 1, 2, 0 }, nearFirst.HopNodes);
        Assert.Equal(2 + 8 + 10, nearFirst.LengthTiles);
        Assert.NotEqual(farFirst.HopNodes, nearFirst.HopNodes);
        AssertHopsAppearInOrder(nearFirst);
    }

    [Fact]
    public void RoundTrip_EmptyStops_StaysAtDepot()
    {
        var graph = LineGraph();
        var route = new VehicleRoute();
        Assert.True(route.TryRoundTrip(graph, new TileCoord(0, 0), new RouteAnchors(), out var trip));
        Assert.Empty(trip.Stops);
        Assert.Equal(new[] { 0, 0 }, trip.HopNodes);
        Assert.Equal(0, trip.LengthTiles);
        Assert.Equal(new[] { 0 }, trip.Nodes);
    }

    [Fact]
    public void RoundTrip_DisconnectedStop_Fails()
    {
        var graph = new RoutingGraph(
            new[]
            {
                new RouteNodeRecord(0, new TileCoord(0, 0)),
                new RouteNodeRecord(1, new TileCoord(2, 0)),
                new RouteNodeRecord(9, new TileCoord(20, 20))
            },
            new[] { new RouteEdgeRecord(0, 1, 2, 0) });
        var anchors = new RouteAnchors();
        anchors.AddAddress(Oak, new TileCoord(20, 20));
        var route = new VehicleRoute();
        route.ReplaceStops(new[] { RouteStop.ForAddress(Oak) });

        Assert.False(route.TryRoundTrip(graph, new TileCoord(0, 0), anchors, out _));
    }

    [Fact]
    public void RoundTrip_UnknownAnchor_Fails()
    {
        var graph = LineGraph();
        var route = new VehicleRoute();
        route.ReplaceStops(new[] { RouteStop.ForAddress(Oak) });
        Assert.False(route.TryRoundTrip(graph, new TileCoord(0, 0), new RouteAnchors(), out _));
    }

    [Fact]
    public void RoundTrip_GeneratedRoutingGraph_PreservesThreeStopOrder()
    {
        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        Assert.True(tables.Valid);
        Assert.True(tables.Houses.Length >= 3);
        Assert.NotEmpty(tables.RouteNodes);
        Assert.NotEmpty(tables.RouteEdges);

        var first = tables.Houses[0];
        var districtHouse = first;
        var constructHouse = tables.Houses[1];
        for (int i = 0; i < tables.Houses.Length; i++)
        {
            if (tables.Houses[i].Address.District != first.Address.District)
            {
                districtHouse = tables.Houses[i];
                break;
            }
        }

        for (int i = 0; i < tables.Houses.Length; i++)
        {
            if (!tables.Houses[i].Address.Equals(first.Address)
                && !tables.Houses[i].Address.Equals(districtHouse.Address))
            {
                constructHouse = tables.Houses[i];
                break;
            }
        }

        var anchors = new RouteAnchors();
        anchors.AddAddress(first.Address, first.Mailbox.Tile(tables.TileCm));
        anchors.AddDistrict(districtHouse.Address.District, districtHouse.Mailbox.Tile(tables.TileCm));
        anchors.AddConstruct(Port, constructHouse.Mailbox.Tile(tables.TileCm));

        var stops = new[]
        {
            RouteStop.ForAddress(first.Address),
            RouteStop.ForDistrict(districtHouse.Address.District),
            RouteStop.ForConstruct(Port)
        };
        var route = new VehicleRoute();
        route.ReplaceStops(stops);

        var graph = new RoutingGraph(tables.RouteNodes, tables.RouteEdges);
        var depot = tables.PostOffice.SpawnPadTile;
        Assert.True(route.TryRoundTrip(graph, depot, anchors, out var trip));
        Assert.Equal(stops, trip.Stops);
        Assert.Equal(depot, trip.Waypoints[0]);
        Assert.Equal(depot, trip.Waypoints[trip.Waypoints.Count - 1]);
        Assert.Equal(trip.HopNodes[0], trip.HopNodes[trip.HopNodes.Count - 1]);
        AssertHopsAppearInOrder(trip);
        Assert.True(trip.LengthTiles > 0);
    }

    private static void AssertHopsAppearInOrder(RouteRoundTrip trip)
    {
        int at = 0;
        for (int i = 0; i < trip.HopNodes.Count; i++)
        {
            int hop = trip.HopNodes[i];
            bool found = false;
            for (int n = at; n < trip.Nodes.Count; n++)
            {
                if (trip.Nodes[n] != hop) continue;
                at = n;
                found = true;
                break;
            }

            Assert.True(found, $"hop {hop} missing after index {at}");
        }
    }

    private static RoutingGraph LineGraph()
    {
        var nodes = new[]
        {
            new RouteNodeRecord(0, new TileCoord(0, 0)),
            new RouteNodeRecord(1, new TileCoord(2, 0)),
            new RouteNodeRecord(2, new TileCoord(10, 0))
        };
        var edges = new[]
        {
            new RouteEdgeRecord(0, 1, 2, 0),
            new RouteEdgeRecord(1, 2, 8, 0)
        };
        return new RoutingGraph(nodes, edges);
    }
}
