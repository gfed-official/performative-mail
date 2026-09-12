namespace PerformativeMail.Net.Tests.UI;

public sealed class MapOverlaySourceTests
{
    [Fact]
    public void MapOverlay_DumpsLayersFiltersAndPings()
    {
        string source = ReadGame("MapOverlay.cs");
        Assert.Contains("MAP_DUMP case=", source);
        Assert.Contains("chip.", source);
        Assert.Contains("districts=", source);
        Assert.Contains("streets=", source);
        Assert.Contains("houses=", source);
        Assert.Contains("resources=", source);
        Assert.Contains("routes=", source);
        Assert.Contains("pings=", source);
        Assert.Contains("pingKind=", source);
        Assert.Contains("PlayTheme.Apply(this)", source);
        Assert.Contains("ToggleChip", source);
        Assert.Contains("TryPlacePing", source);
        Assert.Contains("LivePingRequested", source);
        Assert.Contains("livePings", source);
        Assert.Contains("MapPingBoard", source);
        Assert.Contains("MapCanvas", source);
        Assert.Contains("_GuiInput", source);
        Assert.Contains("MouseButton.Left", source);
        Assert.Contains("DistrictSwatch.Of", source);
        Assert.Contains("Callable.From(() => ToggleChip(captured)).CallDeferred()", source);
        Assert.Contains("OpenRouteEditor", source);
        Assert.Contains("TryClickStop", source);
        Assert.Contains("MoveStop", source);
        Assert.Contains("Route editor", source);
        Assert.Contains("StopRow", source);
        Assert.Contains("DrawDistrictLabels", source);
        Assert.Contains("DrawStops", source);
        Assert.Contains("editor=", source);
    }

    [Fact]
    public void Main_WiresMapToggleAndInspect()
    {
        string main = ReadGame("Main.cs");
        Assert.Contains("BuildMap", main);
        Assert.Contains("PollMapToggle", main);
        Assert.Contains("InputSampler.MapHeld", main);
        Assert.Contains("InspectMap", main);
        Assert.Contains("MapBoot.Tables()", main);
        Assert.Contains("MapBoot.Overlay()", main);
        Assert.Contains("\"--inspect-map\"", main);
        Assert.Contains("\"--map-dump=\"", main);
        Assert.Contains("\"map\"", main);
        Assert.Contains("TryOpenLiveMap", main);
        Assert.Contains("playing.Pings", main);
        Assert.Contains("TryPlacePing", main);
        Assert.Contains("OnLivePingRequested", main);
        Assert.Contains("!_map.IsOpen", main);
    }

    private static string ReadGame(string file)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", file);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("game/" + file);
    }
}
