using PerformativeMail.App;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class ConstructPlacementTests
{
    private const float TileM = 2f;

    [Fact]
    public void Toward_MapsCardinalsOntoViewAxes()
    {
        Assert.Equal((0f, -1f), ConstructPlacement.Toward(Facing.North));
        Assert.Equal((1f, 0f), ConstructPlacement.Toward(Facing.East));
        Assert.Equal((0f, 1f), ConstructPlacement.Toward(Facing.South));
        Assert.Equal((-1f, 0f), ConstructPlacement.Toward(Facing.West));
    }

    [Fact]
    public void Origin_OneByOne_IsTileCenter()
    {
        var origin = ConstructPlacement.Origin(new TileCoord(8, 3), 1, 1, Facing.East, TileM);
        var center = WorldTilePlacement.TileCenter(new TileCoord(8, 3), TileM);
        Assert.Equal(center.X, origin.X, 3);
        Assert.Equal(center.Z, origin.Z, 3);
    }

    [Fact]
    public void VisualTiles_SwapsNonSquareOnEast()
    {
        var size = ConstructPlacement.VisualTiles(2, 1, Facing.East);
        Assert.Equal(new TileCoord(1, 2), size);
        Assert.Equal(new TileCoord(2, 1), ConstructPlacement.VisualTiles(2, 1, Facing.North));
    }

    [Fact]
    public void BoxSize_UsesLockedConstructHeights()
    {
        var belt = ConstructPlacement.BoxSize(BuildingBehaviour.Belt, 1, 1, Facing.East, TileM);
        Assert.Equal(ConstructPlacement.BeltHeightMeters, belt.Y);
        var chest = ConstructPlacement.BoxSize(BuildingBehaviour.Container, 1, 1, Facing.East, TileM);
        Assert.Equal(ConstructPlacement.ChestHeightMeters, chest.Y);
        var wall = ConstructPlacement.BoxSize(BuildingBehaviour.Wall, 1, 1, Facing.East, TileM);
        Assert.Equal(ConstructPlacement.WallHeightMeters, wall.Y);
        var sorter = ConstructPlacement.BoxSize(BuildingBehaviour.Sorter, 2, 2, Facing.East, TileM);
        Assert.Equal(ConstructPlacement.SorterHeightMeters, sorter.Y);
        Assert.True(sorter.X > belt.X);
    }

    [Fact]
    public void LaneItem_AtTileStart_SitsOnFirstTile()
    {
        var tiles = new[] { new TileCoord(8, 3), new TileCoord(9, 3) };
        var at = ConstructPlacement.LaneItem(tiles, Facing.East, 0, lane: 0, tileCm: 200);
        var start = WorldTilePlacement.TileCenter(tiles[0], TileM);
        Assert.True(at.X < start.X);
        Assert.Equal(ConstructPlacement.LaneItemLiftMeters, at.Y);
        Assert.True(Math.Abs(at.Z - start.Z) > 0.2f);
    }
}
