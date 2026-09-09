using PerformativeMail.App;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Content;

namespace PerformativeMail.Net.Tests.App;

public sealed class DebugFactoryTests
{
    [Fact]
    public void Create_AttachesEmptyConstructRegistry()
    {
        var boot = ArcadeSession.Create();
        Assert.NotNull(boot.World.Constructs);
        Assert.Equal(0, boot.World.Constructs.Count);
        Assert.Empty(boot.World.Belts.Segments);
    }

    [Fact]
    public void CreateDebug_SeedsWallChestBeltsAndSorter()
    {
        var boot = ArcadeSession.CreateDebug();
        var constructs = boot.World.Constructs;
        Assert.NotNull(constructs);
        Assert.Equal(7, constructs.Count);
        Assert.True(constructs.TryGetBuilding("wall_wood", out _));
        Assert.Contains(constructs.All, row => row.DefId == "wall_wood" && row.Tile == DebugFactory.WallTile);
        Assert.Contains(constructs.All, row => row.DefId == "chest" && row.Tile == DebugFactory.ChestTile);
        Assert.Contains(constructs.All, row => row.DefId == "address_sorter_mk1" && row.Tile == DebugFactory.SorterTile);
        int belts = 0;
        for (int i = 0; i < constructs.All.Count; i++)
        {
            if (constructs.All[i].DefId == BeltNetwork.BuildingId)
                belts++;
        }

        Assert.Equal(DebugFactory.BeltTiles, belts);
        Assert.NotEmpty(boot.World.Belts.Segments);
        Assert.True(boot.World.Belts.Segments[0].Lane(0).Count >= 1);
    }

    [Fact]
    public void Projector_EmitsViewsAndLaneItemsFromSeed()
    {
        var boot = ArcadeSession.CreateDebug();
        var lanes = new PerformativeMail.Client.LaneReplica();
        if (boot.World.Belts.Segments.Count > 0)
            lanes.Apply(boot.World.Belts.Segments[0].CaptureState(0));

        var frame = ConstructProjector.From(boot.World.Constructs, lanes, advanceDt: 0f);
        Assert.Equal(7, frame.Placed.Count);
        Assert.Contains(frame.Placed, view => view.Behaviour == BuildingBehaviour.Belt);
        Assert.Contains(frame.Placed, view => view.Behaviour == BuildingBehaviour.Container);
        Assert.Contains(frame.Placed, view => view.Behaviour == BuildingBehaviour.Wall);
        Assert.Contains(frame.Placed, view => view.Behaviour == BuildingBehaviour.Sorter);
        Assert.True(frame.LaneItems.Count >= 1);
    }
}
