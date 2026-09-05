using PerformativeMail.App;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class WorldEnvPlacementTests
{
    [Fact]
    public void DebugStreets_AreEastWestTilesWithEndAndSideCurbs()
    {
        var tables = DebugWorld.Tables();
        float tileM = tables.TileCm / 100f;
        var tiles = WorldEnvPlacement.StreetTiles(tables.Streets, tileM);
        var curbs = WorldEnvPlacement.StreetCurbs(tables.Streets, tileM);

        Assert.Equal(16, tiles.Length);
        foreach (var tile in tiles)
            Assert.Equal(MathF.PI * 0.5f, tile.YawRadians);

        Assert.Equal(34, curbs.Length);
        var origin = WorldTilePlacement.TileCenter(new TileCoord(0, 6), tileM);
        Assert.Contains(curbs, c =>
            Nearly(c.X, origin.X - tileM * 0.5f)
            && Nearly(c.Z, origin.Z)
            && Nearly(c.YawRadians, -MathF.PI * 0.5f));
        Assert.Contains(curbs, c =>
            Nearly(c.X, origin.X)
            && Nearly(c.Z, origin.Z - tileM * 0.5f)
            && Nearly(c.YawRadians, MathF.PI));
        Assert.Contains(curbs, c =>
            Nearly(c.X, origin.X)
            && Nearly(c.Z, origin.Z + tileM * 0.5f)
            && Nearly(c.YawRadians, 0f));
    }

    [Fact]
    public void CurbPose_PutsThicknessAxisIntoGrass()
    {
        var at = WorldTilePlacement.TileCenter(new TileCoord(0, 0), 2f);
        var east = WorldEnvPlacement.CurbPose(at, 2f, StreetEdge.East);
        var west = WorldEnvPlacement.CurbPose(at, 2f, StreetEdge.West);
        var north = WorldEnvPlacement.CurbPose(at, 2f, StreetEdge.North);
        var south = WorldEnvPlacement.CurbPose(at, 2f, StreetEdge.South);

        Assert.Equal(2f, east.X);
        Assert.Equal(-1f, east.Z);
        Assert.Equal(MathF.PI * 0.5f, east.YawRadians);
        Assert.Equal(0f, west.X);
        Assert.Equal(-MathF.PI * 0.5f, west.YawRadians);
        Assert.Equal(-2f, north.Z);
        Assert.Equal(MathF.PI, north.YawRadians);
        Assert.Equal(0f, south.Z);
        Assert.Equal(0f, south.YawRadians);
    }

    [Fact]
    public void StreetTileYaw_RotatesOnlyEastWestRuns()
    {
        Assert.Equal(MathF.PI * 0.5f, WorldEnvPlacement.StreetTileYaw(true, true, false, false));
        Assert.Equal(0f, WorldEnvPlacement.StreetTileYaw(false, false, true, true));
        Assert.Equal(0f, WorldEnvPlacement.StreetTileYaw(true, false, true, false));
        Assert.Equal(0f, WorldEnvPlacement.StreetTileYaw(false, false, false, false));
    }

    [Fact]
    public void LotGrass_CoversStartTownLotsAndPoMinusStreets()
    {
        var tables = DebugWorld.Tables();
        float tileM = tables.TileCm / 100f;
        var grass = WorldEnvPlacement.LotGrass(tables.Lots, tables.PostOffice, tables.Streets, tileM);

        Assert.Equal(68, grass.Length);
        var spawn = WorldTilePlacement.TileCenter(tables.PostOffice.SpawnPadTile, tileM);
        Assert.Contains(grass, g => Nearly(g.X, spawn.X) && Nearly(g.Z, spawn.Z));
        var street = WorldTilePlacement.TileCenter(new TileCoord(0, 6), tileM);
        Assert.DoesNotContain(grass, g => Nearly(g.X, street.X) && Nearly(g.Z, street.Z));
    }

    [Fact]
    public void PostalClutter_IsFivePropsOffTheSpawnPad()
    {
        var tables = DebugWorld.Tables();
        float tileM = tables.TileCm / 100f;
        var props = WorldEnvPlacement.PostalClutter(tables.PostOffice, tables.Streets, tileM);
        var spawn = WorldTilePlacement.TileCenter(tables.PostOffice.SpawnPadTile, tileM);
        var intake = WorldTilePlacement.TileCenter(tables.PostOffice.IntakeTile, tileM);

        Assert.Equal(5, props.Length);
        Assert.Equal(3, props.Count(p => p.Kind == EnvPropKind.Crate));
        Assert.Equal(2, props.Count(p => p.Kind == EnvPropKind.Cart));
        foreach (var prop in props)
        {
            float dx = prop.X - spawn.X;
            float dz = prop.Z - spawn.Z;
            Assert.True(dx * dx + dz * dz > 1.2f * 1.2f);
        }

        Assert.Contains(props, p => p.Kind == EnvPropKind.Cart && p.X > intake.X);
        Assert.Contains(props, p => p.Kind == EnvPropKind.Crate && p.Z < -12f);
    }

    [Fact]
    public void YawToward_MapsPlusZForward()
    {
        Assert.Equal(0f, WorldEnvPlacement.YawToward(0f, 1f));
        Assert.Equal(MathF.PI * 0.5f, WorldEnvPlacement.YawToward(1f, 0f));
        Assert.Equal(-MathF.PI * 0.5f, WorldEnvPlacement.YawToward(-1f, 0f));
    }

    private static bool Nearly(float a, float b) => MathF.Abs(a - b) < 1e-4f;
}
