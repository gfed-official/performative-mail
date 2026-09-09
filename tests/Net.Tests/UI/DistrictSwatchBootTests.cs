namespace PerformativeMail.Net.Tests.UI;

public sealed class DistrictSwatchBootTests
{
    [Fact]
    public void GameSwatch_UsesSharedDistrictPalette()
    {
        string source = ReadGame("DistrictSwatch.cs");
        Assert.Contains("DistrictPalette.Rgb", source);
        Assert.Contains("DistrictPalette.IndexOf", source);
        Assert.Contains("DistrictPalette.HexOf", source);
        Assert.Contains("r / 255f, g / 255f, b / 255f", source);
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
