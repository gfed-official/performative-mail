using PerformativeMail.Client;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Net.Tests;

public sealed class VehicleArtTests
{
    [Fact]
    public void PathForVehicle_Bike_ReturnsBikeGlb()
    {
        Assert.Equal("res://art/props/bike_01.glb", VehicleArt.PathForVehicle(VehicleKind.Bike));
    }

    [Fact]
    public void PathForVehicle_MailTruck_ReturnsDedicatedTruckPath()
    {
        string path = VehicleArt.PathForVehicle(VehicleKind.MailTruck);
        Assert.Equal("res://art/props/truck_01.glb", path);
        Assert.Equal(VehicleArt.MailTruck, path);
        Assert.NotEqual(VehicleArt.PathForVehicle(VehicleKind.Bike), path);
    }

    [Fact]
    public void PathForVehicle_Rowboat_ReturnsDedicatedPath()
    {
        string path = VehicleArt.PathForVehicle(VehicleKind.Rowboat);
        Assert.Equal("res://art/props/rowboat_01.glb", path);
        Assert.Equal(VehicleArt.Rowboat, path);
        Assert.NotEqual(VehicleArt.PathForVehicle(VehicleKind.Bike), path);
        var placeholder = VehicleArt.PlaceholderFor(VehicleKind.Rowboat);
        Assert.True(placeholder.LengthMeters > VehicleArt.PlaceholderFor(VehicleKind.Bike).WidthMeters);
    }

    [Fact]
    public void P5BoatPaths_AreReservedOnVehicleArt()
    {
        Assert.Equal("res://art/props/rowboat_01.glb", VehicleArt.Rowboat);
        Assert.Equal("res://art/props/motorboat_01.glb", VehicleArt.Motorboat);
        Assert.NotEqual(VehicleArt.Rowboat, VehicleArt.Motorboat);
        Assert.NotEqual(VehicleArt.MailTruck, VehicleArt.Rowboat);
        Assert.True(VehicleArt.MotorboatPlaceholder.LengthMeters > VehicleArt.RowboatPlaceholder.LengthMeters);
        Assert.True(VehicleArt.RowboatPlaceholder.LengthMeters > VehicleArt.BikePlaceholder.LengthMeters);
    }

    [Fact]
    public void PathForVehicle_UnknownKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VehicleArt.PathForVehicle((VehicleKind)0));
    }

    [Fact]
    public void PlaceholderFor_MailTruck_IsWiderAndLongerThanBike()
    {
        var bike = VehicleArt.PlaceholderFor(VehicleKind.Bike);
        var truck = VehicleArt.PlaceholderFor(VehicleKind.MailTruck);

        Assert.True(truck.WidthMeters > bike.WidthMeters);
        Assert.True(truck.LengthMeters > bike.LengthMeters);
        Assert.False(truck.ColorR == bike.ColorR && truck.ColorG == bike.ColorG && truck.ColorB == bike.ColorB);
    }
}
