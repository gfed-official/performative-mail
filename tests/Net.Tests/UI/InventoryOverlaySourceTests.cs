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

    [Fact]
    public void Slots_StopMouseAndRaiseCellPicked()
    {
        string overlay = ReadGame("InventoryOverlay.cs");
        Assert.Contains("MouseFilter = MouseFilterEnum.Stop", overlay);
        Assert.Contains("MouseFilter = MouseFilterEnum.Ignore", overlay);
        Assert.Contains("slot.GuiInput +=", overlay);
        Assert.Contains("CellPicked?.Invoke", overlay);
        Assert.Contains("SelectCell", overlay);
        Assert.Contains("MouseButton.Left", overlay);
    }

    [Fact]
    public void MailCells_AddDistrictSwatchBesideAddressText()
    {
        string overlay = ReadGame("InventoryOverlay.cs");
        Assert.Contains("DistrictPalette.HasSwatch(cell.District)", overlay);
        Assert.Contains("DistrictSwatch.Of(cell.District)", overlay);
        Assert.Contains("_swatch", overlay);
        Assert.Contains("cell.Text", overlay);
        Assert.Contains("district=", overlay);
        Assert.Contains("swatch=", overlay);
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
