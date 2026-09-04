namespace PerformativeMail.Net.Tests.UI;

public sealed class WorldStageBootTests
{
    [Fact]
    public void WorldStage_LabelsPostOfficeMailAndAddresses()
    {
        var source = ReadWorldStage();
        Assert.Contains("Post Office", source);
        Assert.Contains("\"Mail\"", source);
        Assert.Contains("MailboxPrefix", source);
        Assert.Contains("AddressText.Format", source);
        Assert.Contains("AddLabeledBox", source);
        Assert.Contains("Sync(WorldTables", source);
        Assert.Contains("WorldLabelPlacement.AboveStreetFace", source);
        Assert.Contains("WorldTilePlacement.TileCenter", source);
        Assert.Contains("WorldTilePlacement.FootprintOrigin", source);
        Assert.Contains("WorldTilePlacement.TowardNearestStreet", source);
        Assert.Contains("Billboard = BaseMaterial3D.BillboardModeEnum.Enabled", source);
        Assert.DoesNotContain("new Vector3(0f, 1.6f, 0f)", source);
        Assert.DoesNotContain("new Vector3(0f, 1.4f, 0f)", source);
        Assert.DoesNotContain("new Vector3(0f, 1.0f, 0f)", source);
    }

    [Fact]
    public void WorldStage_DumpWalksLiveLabel3DText()
    {
        var source = ReadWorldStage();
        Assert.Contains("public string Dump()", source);
        Assert.Contains("WORLD_DUMP", source);
        Assert.Contains("GetNodeOrNull<Label3D>(\"Label\")", source);
        Assert.Contains("Label=", source);
        Assert.Contains("WORLD_DUMP_END", source);
        Assert.DoesNotContain("ReadAllText", source);
    }

    private static string ReadWorldStage()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", "WorldStage.cs");
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("game/WorldStage.cs");
    }
}
