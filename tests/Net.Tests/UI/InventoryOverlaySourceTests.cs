namespace PerformativeMail.Net.Tests.UI;

public sealed class InventoryOverlaySourceTests
{
    [Fact]
    public void ClearColumn_FreesFromFront_NotWhileEnumeratingGetChildren()
    {
        string overlay = ReadGame("InventoryOverlay.cs");

        Assert.Contains("while (column.GetChildCount() > 0)", overlay);
        Assert.Contains("column.GetChild(0).Free()", overlay);
        Assert.DoesNotContain("foreach (var child in column.GetChildren())", overlay);
    }

    [Fact]
    public void Dump_SkipsInvalidCellLabels()
    {
        string overlay = ReadGame("InventoryOverlay.cs");
        Assert.Contains("GodotObject.IsInstanceValid(pair.Value)", overlay);
        Assert.Contains("grid.Cells is null || grid.Cells.Count == 0", overlay);
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
