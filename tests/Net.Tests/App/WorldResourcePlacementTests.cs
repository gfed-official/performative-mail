using PerformativeMail.App;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class WorldResourcePlacementTests
{
    [Theory]
    [InlineData(ResourceKind.Wood, "Wood")]
    [InlineData(ResourceKind.Fiber, "Fiber")]
    [InlineData(ResourceKind.Stone, "Stone")]
    [InlineData(ResourceKind.IronOre, "Iron Ore")]
    [InlineData(ResourceKind.Sand, "Sand")]
    [InlineData(ResourceKind.Berries, "Berries")]
    public void Label_Live_UsesKindName(ResourceKind kind, string label)
    {
        Assert.Equal(label, WorldResourcePlacement.Label(kind, HarvestRemnant.Live));
    }

    [Fact]
    public void Label_Stump_ReadsStump()
    {
        Assert.Equal("Stump", WorldResourcePlacement.Label(ResourceKind.Wood, HarvestRemnant.Stump));
    }

    [Fact]
    public void NodeName_UsesPrefixAndTile()
    {
        Assert.Equal("Resource_4_9", WorldResourcePlacement.NodeName(new TileCoord(4, 9)));
        Assert.Equal("Resource_", WorldResourcePlacement.NodePrefix);
    }

    [Theory]
    [InlineData(HarvestRemnant.Live, true)]
    [InlineData(HarvestRemnant.Stump, true)]
    [InlineData(HarvestRemnant.Gone, false)]
    [InlineData(HarvestRemnant.RegrowNextShift, false)]
    public void IsMarkerVisible_FollowsRemnant(HarvestRemnant remnant, bool visible)
    {
        Assert.Equal(visible, WorldResourcePlacement.IsMarkerVisible(remnant));
    }

    [Fact]
    public void BoxSize_StumpIsShorterThanLiveWood()
    {
        var live = WorldResourcePlacement.BoxSize(ResourceKind.Wood, HarvestRemnant.Live);
        var stump = WorldResourcePlacement.BoxSize(ResourceKind.Wood, HarvestRemnant.Stump);
        Assert.True(stump.Y < live.Y);
        Assert.Equal(2.20f, live.Y);
        Assert.Equal(0.40f, stump.Y);
    }

    [Fact]
    public void ColorRgb_MatchesLockedPlaceholderSwatches()
    {
        Assert.Equal((0.24f, 0.42f, 0.18f), WorldResourcePlacement.ColorRgb(ResourceKind.Wood, HarvestRemnant.Live));
        Assert.Equal((0.35f, 0.24f, 0.14f), WorldResourcePlacement.ColorRgb(ResourceKind.Wood, HarvestRemnant.Stump));
        Assert.Equal((0.54f, 0.53f, 0.50f), WorldResourcePlacement.ColorRgb(ResourceKind.Stone, HarvestRemnant.Live));
    }
}
