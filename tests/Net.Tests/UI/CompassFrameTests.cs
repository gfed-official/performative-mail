using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.UI;

public sealed class CompassFrameTests
{
    private const ushort South = 32768;
    private const ushort East = 16384;
    private const ushort West = 49152;

    [Fact]
    public void Empty_IsNorthWithNoDistricts()
    {
        var frame = CompassFrame.Empty;

        Assert.Equal((byte)0, frame.FacingEighth);
        Assert.Equal("N", frame.FacingLabel);
        Assert.Equal((byte)0, frame.CurrentDistrict);
        Assert.Equal(CompassFrame.SlotCount, frame.Slots.Count);
        Assert.Equal(new string('0', CompassFrame.SlotCount), frame.SlotKey);
    }

    [Fact]
    public void Eighth_RoundsYawToEightWinds()
    {
        Assert.Equal((byte)0, CompassFrame.Eighth(0));
        Assert.Equal((byte)0, CompassFrame.Eighth(4095));
        Assert.Equal((byte)1, CompassFrame.Eighth(4096));
        Assert.Equal((byte)2, CompassFrame.Eighth(East));
        Assert.Equal((byte)4, CompassFrame.Eighth(South));
        Assert.Equal((byte)6, CompassFrame.Eighth(West));
        Assert.Equal((byte)0, CompassFrame.Eighth(65535));
        Assert.Equal("N", CompassFrame.WindName(0));
        Assert.Equal("SE", CompassFrame.WindName(3));
        Assert.Equal("NW", CompassFrame.WindName(7));
    }

    [Fact]
    public void From_NullWorld_KeepsFacingWithoutDistricts()
    {
        var frame = CompassFrame.From(null, new PlayerPose(0, 0, 0, South));

        Assert.Equal((byte)4, frame.FacingEighth);
        Assert.Equal("S", frame.FacingLabel);
        Assert.Equal((byte)0, frame.CurrentDistrict);
        Assert.Equal(new string('0', CompassFrame.SlotCount), frame.SlotKey);
    }

    [Fact]
    public void From_DebugSpawn_NorthLooksAtDistrictOne()
    {
        var tables = DebugWorld.Tables();
        var pose = SpawnRing.CentreOf(WorldAtlas.FromTables(tables));
        var frame = CompassFrame.From(tables, pose);
        var boot = CompassBoot.Placeholder();

        Assert.Equal((byte)0, frame.FacingEighth);
        Assert.Equal("N", frame.FacingLabel);
        Assert.Equal((byte)1, frame.CurrentDistrict);
        Assert.Contains('1', frame.SlotKey);
        Assert.Equal("000000000011111110000000", frame.SlotKey);
        Assert.Equal((byte)1, frame.Slots[CompassFrame.SlotCount / 2]);
        Assert.True(CompassFrame.SameDisplay(in frame, in boot));
        Assert.Equal(boot.SlotKey, frame.SlotKey);
    }

    [Fact]
    public void From_FaceSouth_MovesDistrictBandOffCenter()
    {
        var tables = DebugWorld.Tables();
        var spawn = SpawnRing.CentreOf(WorldAtlas.FromTables(tables));
        var north = CompassFrame.From(tables, spawn);
        var south = CompassFrame.From(tables, new PlayerPose(spawn.Xcm, spawn.Ycm, spawn.Zcm, South));

        Assert.Equal("S", south.FacingLabel);
        Assert.Equal((byte)1, south.CurrentDistrict);
        Assert.Contains('1', south.SlotKey);
        Assert.Equal((byte)0, south.Slots[CompassFrame.SlotCount / 2]);
        Assert.False(CompassFrame.SameDisplay(in north, in south));
    }

    [Fact]
    public void From_EastStreet_AppearsOnTheRightWhenFacingNorth()
    {
        var tables = TwoDistricts();
        var pose = new PlayerPose(500, 500, 0, 0);
        var frame = CompassFrame.From(tables, pose);

        Assert.Equal((byte)1, frame.CurrentDistrict);
        Assert.Equal((byte)1, frame.Slots[CompassFrame.SlotCount / 2]);
        Assert.Equal((byte)2, frame.Slots[18]);
    }

    [Fact]
    public void From_FarStreet_IsIgnoredForNearbyBand()
    {
        var far = new StreetRecord(2, "Far", 2, new[] { new TileCoord(80, 80) });
        var tables = WithStreets(DebugWorld.Tables().Streets[0], far);
        var pose = new PlayerPose(500, 500, 0, 0);
        var frame = CompassFrame.From(tables, pose);

        Assert.Equal((byte)1, frame.CurrentDistrict);
        Assert.DoesNotContain('2', frame.SlotKey);
    }

    [Fact]
    public void SameDisplay_TrueWhenYawStaysInTheSameBand()
    {
        var tables = DebugWorld.Tables();
        var spawn = SpawnRing.CentreOf(WorldAtlas.FromTables(tables));
        var a = CompassFrame.From(tables, spawn);
        var b = CompassFrame.From(tables, new PlayerPose(spawn.Xcm, spawn.Ycm, spawn.Zcm, 200));

        Assert.True(CompassFrame.SameDisplay(in a, in b));
        Assert.Equal(a.SlotKey, b.SlotKey);
    }

    private static WorldTables TwoDistricts()
    {
        var north = DebugWorld.Tables().Streets[0];
        var east = new StreetRecord(2, "East Lane", 2, new[] { new TileCoord(12, 2) });
        return WithStreets(north, east);
    }

    private static WorldTables WithStreets(params StreetRecord[] streets)
    {
        var tables = DebugWorld.Tables();
        return new WorldTables(
            tables.Width,
            tables.Height,
            tables.TileCm,
            tables.Heights,
            tables.Houses,
            tables.Buildable,
            tables.HeightmapAttempts,
            tables.PostOffice,
            streets,
            tables.Lots,
            tables.ResourceNodes,
            tables.Ferries,
            tables.RouteNodes,
            tables.RouteEdges,
            tables.SpawnEdges,
            tables.ValidationAttempts);
    }
}
