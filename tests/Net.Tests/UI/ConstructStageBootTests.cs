namespace PerformativeMail.Net.Tests.UI;

public sealed class ConstructStageBootTests
{
    [Fact]
    public void ConstructStage_RendersLockedPrimitivesFromConstructFrame()
    {
        var source = ReadGame("ConstructStage.cs");
        Assert.Contains("class ConstructStage", source);
        Assert.Contains("Sync(in ConstructFrame frame)", source);
        Assert.Contains("ConstructPlacement.BoxSize", source);
        Assert.Contains("ConstructPlacement.Origin", source);
        Assert.Contains("ConstructPlacement.Toward", source);
        Assert.Contains("ConstructPlacement.LaneItem", source);
        Assert.Contains("BoxMesh", source);
        Assert.Contains("#C4A84A", source);
        Assert.Contains("#8B5A2B", source);
        Assert.Contains("#A67C52", source);
        Assert.Contains("#3D7EFF", source);
        Assert.Contains("#F7F1DE", source);
        Assert.Contains("Prefix = \"Construct_\"", source);
        Assert.Contains("LanePrefix = \"LaneItem_\"", source);
        Assert.Contains("CONSTRUCT_DUMP", source);
        Assert.Contains("GetNodeOrNull<Label3D>(\"Label\")", source);
        Assert.Contains("BuildingBehaviour.Belt", source);
        Assert.Contains("BuildingBehaviour.Container", source);
        Assert.Contains("BuildingBehaviour.Wall", source);
        Assert.Contains("BuildingBehaviour.Sorter", source);
        Assert.Contains("ArgumentOutOfRangeException", source);
        Assert.DoesNotContain("QuadMesh", source);
        Assert.DoesNotContain("Colors.Magenta", source);
    }

    private static string ReadGame(string fileName)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", fileName);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("game/" + fileName);
    }
}
