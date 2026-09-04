using PerformativeMail.App;
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
}
