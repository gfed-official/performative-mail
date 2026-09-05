using PerformativeMail.App;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class WorldTilePlacementTests
{
    [Fact]
    public void TileCenter_MapsOriginTileToViewMetres()
    {
        var at = WorldTilePlacement.TileCenter(new TileCoord(0, 0), 2f);
        Assert.Equal(1f, at.X);
        Assert.Equal(0f, at.Y);
        Assert.Equal(-1f, at.Z);
    }

    [Fact]
    public void TileCenter_NegatesNorthAndKeepsEast()
    {
        var at = WorldTilePlacement.TileCenter(new TileCoord(5, 3), 2f);
        Assert.Equal(11f, at.X);
        Assert.Equal(0f, at.Y);
        Assert.Equal(-7f, at.Z);
    }

    [Fact]
    public void FootprintOrigin_UsesFootprintCentre()
    {
        var at = WorldTilePlacement.FootprintOrigin(new TileCoord(0, 0), new TileCoord(6, 6), 2f);
        Assert.Equal(6f, at.X);
        Assert.Equal(0f, at.Y);
        Assert.Equal(-6f, at.Z);
    }

    [Fact]
    public void TowardNearestStreet_PicksClosestTileInViewSpace()
    {
        var streets = new[]
        {
            new StreetRecord(1, "Oak", 1, new[] { new TileCoord(1, 0), new TileCoord(0, 2) }),
        };
        var toward = WorldTilePlacement.TowardNearestStreet(0f, 0f, streets, 2f);
        Assert.Equal(3f, toward.X);
        Assert.Equal(-1f, toward.Z);
    }

    [Fact]
    public void TowardNearestStreet_EmptyStreets_ReturnsZero()
    {
        var toward = WorldTilePlacement.TowardNearestStreet(4f, -2f, Array.Empty<StreetRecord>(), 2f);
        Assert.Equal(0f, toward.X);
        Assert.Equal(0f, toward.Z);
    }

    [Fact]
    public void SmallIslandGround_CoversViewFrameTownInNegativeZ()
    {
        var slab = WorldTilePlacement.SmallIslandGround();
        Assert.True(slab.Z < 0f);
        Assert.Equal(-WorldTilePlacement.GroundThicknessM * 0.5f, slab.Y);
        Assert.Equal(WorldTilePlacement.GroundThicknessM, slab.SizeY);

        float tileM = WorldGen.SmallIslandTileCm / 100f;
        AssertCovered(slab, WorldTilePlacement.TileCenter(new TileCoord(0, 0), tileM));
        AssertCovered(
            slab,
            WorldTilePlacement.TileCenter(
                new TileCoord(WorldGen.SmallIslandTiles - 1, WorldGen.SmallIslandTiles - 1),
                tileM));
        AssertCovered(slab, WorldTilePlacement.TileCenter(DebugWorld.PostOfficeTile, tileM));
        AssertCovered(slab, WorldTilePlacement.TileCenter(DebugWorld.IntakeTile, tileM));
        AssertCovered(
            slab,
            WorldTilePlacement.FootprintOrigin(DebugWorld.House1Lot, DebugWorld.LotSize, tileM));
        AssertCovered(
            slab,
            WorldTilePlacement.FootprintOrigin(DebugWorld.House2Lot, DebugWorld.LotSize, tileM));
        var mailbox = ViewFrame.From(new PlayerPose(
            DebugWorld.House1Mailbox.XCm,
            DebugWorld.House1Mailbox.YCm,
            DebugWorld.House1Mailbox.ZCm,
            0));
        AssertCovered(slab, (mailbox.X, 0f, mailbox.Z));
    }

    [Fact]
    public void SmallIslandGround_DoesNotSitInPositiveZOnly()
    {
        var slab = WorldTilePlacement.SmallIslandGround();
        float halfZ = slab.SizeZ * 0.5f;
        Assert.True(slab.Z - halfZ < 0f);
        Assert.True(slab.Z + halfZ <= 0f);
        Assert.InRange(slab.X, 0f, slab.SizeX);
    }

    private static void AssertCovered(GroundSlab slab, (float X, float Y, float Z) at)
    {
        float hx = slab.SizeX * 0.5f;
        float hz = slab.SizeZ * 0.5f;
        Assert.InRange(at.X, slab.X - hx, slab.X + hx);
        Assert.InRange(at.Z, slab.Z - hz, slab.Z + hz);
    }
}
