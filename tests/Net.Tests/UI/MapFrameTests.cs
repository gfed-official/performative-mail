using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.UI;

public sealed class MapFrameTests
{
    [Fact]
    public void From_MapBoot_ProjectsDistrictStreetHouseResourceAndRoute()
    {
        var frame = MapBoot.Placeholder();

        Assert.Equal(MapBoot.Width, frame.Width);
        Assert.Equal(MapBoot.Height, frame.Height);
        Assert.Equal(MapFrame.DefaultLayers, frame.Layers);
        Assert.Equal(MapFilter.None, frame.Filters);
        Assert.Equal(2, frame.Districts.Count);
        Assert.Equal("#3D7EFF", frame.Districts[0].Hex);
        Assert.Equal("#E85D3A", frame.Districts[1].Hex);
        Assert.Equal(2, frame.Streets.Count);
        Assert.Equal(MapBoot.StreetA, frame.Streets[0].Name);
        Assert.Equal(MapBoot.StreetB, frame.Streets[1].Name);
        Assert.Equal(2, frame.Houses.Count);
        Assert.Equal("1/1/1", frame.Houses[0].Label);
        Assert.True(frame.Houses[0].HasMail);
        Assert.Equal("2/2/2", frame.Houses[1].Label);
        Assert.False(frame.Houses[1].HasMail);
        Assert.Empty(frame.Pings);
        Assert.Single(frame.Resources);
        Assert.Equal("wood", frame.Resources[0].Label);
        Assert.Equal(MapBoot.WoodTile, frame.Resources[0].Tile);
        Assert.Single(frame.Routes);
        Assert.Equal(1, frame.Routes[0].From);
        Assert.Equal(2, frame.Routes[0].To);
        Assert.True(Chip(frame, "districts"));
        Assert.True(Chip(frame, "streets"));
        Assert.False(Chip(frame, "mail"));
        Assert.False(Chip(frame, "routes"));
        Assert.False(Chip(frame, "resources"));
    }

    [Fact]
    public void From_DebugWorld_UsesStreetAndHouseDistricts()
    {
        var frame = MapFrame.From(
            DebugWorld.Tables(),
            null,
            MapFrame.DefaultLayers,
            MapFilter.None,
            Array.Empty<MapPing>());

        Assert.Equal(DebugWorld.Width, frame.Width);
        Assert.Equal(DebugWorld.StreetName, Assert.Single(frame.Streets).Name);
        Assert.Equal((byte)1, Assert.Single(frame.Districts).District);
        Assert.Equal(2, frame.Houses.Count);
        Assert.False(frame.Houses[0].HasMail);
        Assert.Empty(frame.Resources);
        Assert.Empty(frame.Routes);
    }

    [Fact]
    public void From_NullWorld_IsEmptyWithChips()
    {
        var frame = MapFrame.From(null, null, MapLayer.Districts, MapFilter.Mail, Array.Empty<MapPing>());
        Assert.Equal(0, frame.Width);
        Assert.Empty(frame.Districts);
        Assert.True(Chip(frame, "districts"));
        Assert.True(Chip(frame, "mail"));
        Assert.False(Chip(frame, "streets"));
    }

    [Fact]
    public void From_FilterChips_ReflectMailRoutesResources()
    {
        var filters = MapFilter.Mail | MapFilter.Routes | MapFilter.Resources;
        var frame = MapFrame.From(
            MapBoot.Tables(),
            MapBoot.Overlay(),
            MapLayer.Streets,
            filters,
            Array.Empty<MapPing>());

        Assert.Equal(MapLayer.Streets, frame.Layers);
        Assert.Equal(filters, frame.Filters);
        Assert.False(Chip(frame, "districts"));
        Assert.True(Chip(frame, "streets"));
        Assert.True(Chip(frame, "mail"));
        Assert.True(Chip(frame, "routes"));
        Assert.True(Chip(frame, "resources"));
        Assert.True(frame.Houses[0].HasMail);
    }

    [Fact]
    public void From_Pings_ProjectKindKeys()
    {
        var pings = new[]
        {
            new MapPing(1, MapBoot.PingTile, MapPingKind.Danger, 0),
            new MapPing(2, new TileCoord(1, 2), MapPingKind.DeliverHere, 10),
        };
        var frame = MapFrame.From(
            MapBoot.Tables(),
            null,
            MapFrame.DefaultLayers,
            MapFilter.None,
            pings);

        Assert.Equal(2, frame.Pings.Count);
        Assert.Equal("danger", frame.Pings[0].Key);
        Assert.Equal(MapBoot.PingTile, frame.Pings[0].Tile);
        Assert.Equal("deliver", frame.Pings[1].Key);
    }

    [Fact]
    public void SameDisplay_IgnoresUnrelatedPingTickButSeesNewPing()
    {
        var world = MapBoot.Tables();
        var overlay = MapBoot.Overlay();
        var first = MapFrame.From(world, overlay, MapFrame.DefaultLayers, MapFilter.None, Array.Empty<MapPing>());
        var same = MapFrame.From(world, overlay, MapFrame.DefaultLayers, MapFilter.None, Array.Empty<MapPing>());
        Assert.True(MapFrame.SameDisplay(in first, in same));

        var withPing = MapFrame.From(
            world,
            overlay,
            MapFrame.DefaultLayers,
            MapFilter.None,
            new[] { new MapPing(1, MapBoot.PingTile, MapPingKind.Default, 0) });
        Assert.False(MapFrame.SameDisplay(in first, in withPing));
    }

    [Fact]
    public void MailAddresses_ReadsHotbarLetter()
    {
        var mail = MapFrame.MailAddresses(MapBoot.Overlay());
        Assert.Contains(MapBoot.HouseA, mail);
        Assert.DoesNotContain(MapBoot.HouseB, mail);
    }

    private static bool Chip(in MapFrame frame, string id)
    {
        foreach (var chip in frame.Chips)
        {
            if (chip.Id == id)
                return chip.On;
        }

        throw new InvalidOperationException("missing chip " + id);
    }
}
