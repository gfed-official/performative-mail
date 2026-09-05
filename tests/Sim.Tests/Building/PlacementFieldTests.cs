using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Building;

public sealed class PlacementFieldTests
{
    [Fact]
    public void TryPlanFlatten_U82Slope_RaisesFootprintBy1cm()
    {
        var field = PlacementField.Flat(3, 3, 200, 100).WithHeight(new TileCoord(2, 1), 154);
        var tiles = new[] { new TileCoord(1, 1) };

        Assert.True(field.TryPlanFlatten(tiles, out var planned));

        Assert.Equal(new FlattenedTile(1, 1, 101), Assert.Single(planned));
        Assert.Equal(100, field.HeightAt(new TileCoord(1, 1)));
    }

    [Fact]
    public void ApplyFlatten_WritesPlannedHeight()
    {
        var field = PlacementField.Flat(3, 3, 200, 100).WithHeight(new TileCoord(2, 1), 154);
        var tiles = new[] { new TileCoord(1, 1) };
        Assert.True(field.TryPlanFlatten(tiles, out var planned));

        field.ApplyFlatten(planned);

        Assert.Equal(101, field.HeightAt(new TileCoord(1, 1)));
        Assert.False(field.SlopeExceeds(new TileCoord(1, 1)));
    }

    [Fact]
    public void TryPlanFlatten_Over1m_FailsAndLeavesHeights()
    {
        var field = PlacementField.Flat(3, 3, 200, 100).WithHeight(new TileCoord(2, 1), 254);
        var tiles = new[] { new TileCoord(1, 1) };

        Assert.False(field.TryPlanFlatten(tiles, out var planned));

        Assert.Empty(planned);
        Assert.Equal(100, field.HeightAt(new TileCoord(1, 1)));
        Assert.True(field.SlopeExceeds(new TileCoord(1, 1)));
    }

    [Fact]
    public void TryPlanFlatten_TwoTilePad_OneHeight()
    {
        var field = PlacementField.Flat(4, 3, 200, 100).WithHeight(new TileCoord(2, 1), 120);
        var tiles = new[] { new TileCoord(1, 1), new TileCoord(2, 1) };

        Assert.True(field.TryPlanFlatten(tiles, out var planned));

        Assert.Equal(2, planned.Length);
        Assert.Equal(new FlattenedTile(1, 1, 110), planned[0]);
        Assert.Equal(new FlattenedTile(2, 1, 110), planned[1]);
    }
}
