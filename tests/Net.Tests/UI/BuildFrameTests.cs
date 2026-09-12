using PerformativeMail.App;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.UI;

public sealed class BuildFrameTests
{
    [Fact]
    public void Open_SelectsFirstTransportAndListsSpecCategories()
    {
        var mode = new BuildModeState(LoadBuildings());
        mode.Open();
        var frame = mode.Frame(true, "");

        Assert.True(frame.Open);
        Assert.Equal(BuildCategory.Transport, frame.Category);
        Assert.Equal("belt_mk1", frame.SelectedId);
        Assert.Equal("Conveyor Belt", frame.SelectedName);
        Assert.Equal(new[] { "Transport", "Sorting", "Storage", "Vehicles", "Defense", "Extractors" },
            frame.Categories.Select(tab => tab.Label).ToArray());
        Assert.Contains(frame.Choices, choice => choice.Id == "belt_mk1" && choice.Selected);
    }

    [Fact]
    public void Select_Pier_SwitchesToVehicles()
    {
        var mode = new BuildModeState(LoadBuildings());
        Assert.True(mode.Select("pier"));
        var frame = mode.Frame(true, "");

        Assert.Equal(BuildCategory.Vehicles, frame.Category);
        Assert.Equal("pier", frame.SelectedId);
        Assert.Equal("Pier", frame.SelectedName);
        Assert.Contains(frame.Choices, choice => choice.Id == "pier" && choice.Selected);
    }

    [Fact]
    public void Select_Wall_SwitchesToDefense()
    {
        var mode = new BuildModeState(LoadBuildings());
        Assert.True(mode.Select("wall_wood"));
        var frame = mode.Frame(false, BuildRejectText.Of(PlaceReject.Street));

        Assert.Equal(BuildCategory.Defense, frame.Category);
        Assert.Equal("wall_wood", frame.SelectedId);
        Assert.Equal("Wooden Wall", frame.SelectedName);
        Assert.False(frame.Valid);
        Assert.Equal("On street", frame.Reason);
    }

    [Fact]
    public void PipetteAndRotate_UpdateSelection()
    {
        var mode = new BuildModeState(LoadBuildings());
        Assert.True(mode.TryPipette("chest"));
        mode.Rotate();
        var frame = mode.Frame(true, "");

        Assert.Equal(BuildCategory.Storage, frame.Category);
        Assert.Equal("chest", frame.SelectedId);
        Assert.Equal(Facing.East, frame.Facing);
    }

    [Fact]
    public void RejectText_MatchesSpecPhrases()
    {
        Assert.Equal("On street", BuildRejectText.Of(PlaceReject.Street));
        Assert.Equal("Too steep", BuildRejectText.Of(PlaceReject.Slope));
        Assert.Equal("Needs deep water", BuildRejectText.Of(PlaceReject.Water));
        Assert.Equal("Needs shallow water", BuildRejectText.Of(PlaceReject.DryLand));
        Assert.Equal("Missing input", BuildRejectText.Of(PlaceReject.MissingInput));
    }

    private static BuildingDef[] LoadBuildings()
    {
        var bundle = ContentBoot.Load(out _, out _);
        return bundle.Buildings;
    }
}
