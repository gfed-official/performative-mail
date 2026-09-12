using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Net.Tests;

public sealed class VehicleViewTableTests
{
    [Fact]
    public void Refresh_ReplicaTable_ReportsMountedVehicleKind()
    {
        var client = ConnectedClient();
        ContentBoot.Load(out _, out var catalog);
        var vehicles = new VehicleTable();
        vehicles.SpawnBike(PlayerPose.Origin);
        vehicles.SpawnMailTruck(PlayerPose.FromMeters(4.0, 0.0, 0.0, 0), new InventorySystem(catalog));

        var table = new VehicleViewTable();
        table.Refresh(client, vehicles, TimeSpan.Zero);

        Assert.Equal(2, table.Visible.Count);
        Assert.Equal(VehicleKind.Bike, table.Visible[0].Kind);
        Assert.Equal(VehicleKind.MailTruck, table.Visible[1].Kind);
    }

    [Fact]
    public void Refresh_PredictedMount_ReportsPredictionVehicleKind()
    {
        var client = ConnectedClient();
        var truckId = EntityId.FromClassAndCounter(EntityClass.Vehicle, 9);
        client.Prediction.Mount(truckId, VehicleContext.MailTruckOnRoad);

        var table = new VehicleViewTable();
        table.Refresh(client, vehicles: null, TimeSpan.Zero);

        var view = Assert.Single(table.Visible);
        Assert.Equal(truckId, view.Id);
        Assert.Equal(VehicleKind.MailTruck, view.Kind);
    }

    [Fact]
    public void Refresh_PredictedBike_StillReportsBike()
    {
        var client = ConnectedClient();
        var bikeId = EntityId.FromClassAndCounter(EntityClass.Vehicle, 3);
        client.Prediction.Mount(bikeId, VehicleContext.BikeOnRoad);

        var table = new VehicleViewTable();
        table.Refresh(client, vehicles: null, TimeSpan.Zero);

        var view = Assert.Single(table.Visible);
        Assert.Equal(VehicleKind.Bike, view.Kind);
    }

    private static ClientRuntime ConnectedClient()
    {
        var (server, client, _) = Boot.CreateListenHost();
        server.TickOnce();
        client.Receive();
        Assert.True(client.LocalPlayer.HasValue);
        Assert.True(client.TryPresent(client.LocalPlayer.Value, TimeSpan.Zero, out _));
        return client;
    }
}
