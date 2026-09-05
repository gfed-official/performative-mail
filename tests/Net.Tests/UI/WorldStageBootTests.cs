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
        Assert.Contains("ArtMesh.TryInstantiate", source);
        Assert.Contains("ArtMesh.PostOffice", source);
        Assert.Contains("ArtMesh.Intake", source);
        Assert.Contains("ArtMesh.Mailbox", source);
        Assert.Contains("ArtMesh.HouseVariant", source);
        Assert.Contains("ArtMesh.StreetTile", source);
        Assert.Contains("ArtMesh.StreetCurb", source);
        Assert.Contains("ArtMesh.SpawnPad", source);
        Assert.Contains("ArtMesh.GrassTile", source);
        Assert.Contains("ArtMesh.PathForProp", source);
        Assert.Contains("WorldEnvPlacement.StreetTiles", source);
        Assert.Contains("WorldEnvPlacement.StreetCurbs", source);
        Assert.Contains("WorldEnvPlacement.LotGrass", source);
        Assert.Contains("WorldEnvPlacement.PostalClutter", source);
        Assert.Contains("Sync(WorldTables", source);
        Assert.Contains("WorldLabelPlacement.AboveStreetFace", source);
        Assert.Contains("WorldTilePlacement.TileCenter", source);
        Assert.Contains("WorldTilePlacement.FootprintOrigin", source);
        Assert.Contains("WorldTilePlacement.TowardNearestStreet", source);
        Assert.Contains("Billboard = BaseMaterial3D.BillboardModeEnum.Enabled", source);
        Assert.Contains("OutlineSize = LabelOutlineSize", source);
        Assert.Contains("PixelSize = LabelPixelSize", source);
        Assert.Contains("LabelOutlineSize = 8", source);
        Assert.Contains("LabelPixelSize = 0.01f", source);
        Assert.Contains("Modulate = Colors.White", source);
        Assert.DoesNotContain("OutlineSize = 6", source);
        Assert.DoesNotContain("QuadMesh", source);
        Assert.DoesNotContain("new Vector3(0f, 1.6f, 0f)", source);
        Assert.DoesNotContain("new Vector3(0f, 1.4f, 0f)", source);
        Assert.DoesNotContain("new Vector3(0f, 1.0f, 0f)", source);
    }

    [Fact]
    public void WorldStage_UsesLockedPaletteRoofsAndFlags()
    {
        var source = ReadWorldStage();
        Assert.Contains("#A04B3A", source);
        Assert.Contains("0.63f, 0.29f, 0.23f", source);
        Assert.Contains("#C4A84A", source);
        Assert.Contains("0.77f, 0.66f, 0.29f", source);
        Assert.Contains("#F2D24A", source);
        Assert.Contains("0.95f, 0.82f, 0.29f", source);
        Assert.Contains("#5A5C66", source);
        Assert.Contains("0.35f, 0.36f, 0.40f", source);
        Assert.Contains("#8A8E9A", source);
        Assert.Contains("0.54f, 0.56f, 0.60f", source);
        Assert.Contains("#E0CFA8", source);
        Assert.Contains("0.88f, 0.81f, 0.66f", source);
        Assert.Contains("#6B4E6E", source);
        Assert.Contains("0.42f, 0.31f, 0.43f", source);
        Assert.Contains("#2F3A8C", source);
        Assert.Contains("0.18f, 0.23f, 0.55f", source);
        Assert.Contains("#E85D3A", source);
        Assert.Contains("0.91f, 0.36f, 0.23f", source);
        Assert.Contains("AddHouseRoof", source);
        Assert.Contains("WorldPropPlacement.HouseRoofHeightMeters", source);
        Assert.Contains("WorldPropPlacement.RoofSize", source);
        Assert.Contains("AddMailboxFlag", source);
        Assert.Contains("WorldPropPlacement.MailboxFlagSize", source);
        Assert.Contains("WorldPropPlacement.MailboxFlagOffset", source);
        Assert.Contains("Name = \"Roof\"", source);
        Assert.Contains("Name = \"Flag\"", source);
        Assert.DoesNotContain("new Color(0.55f, 0.28f, 0.22f)", source);
        Assert.DoesNotContain("new Color(0.72f, 0.62f, 0.28f)", source);
        Assert.DoesNotContain("new Color(0.95f, 0.82f, 0.2f)", source);
        Assert.DoesNotContain("new Color(0.38f, 0.38f, 0.4f)", source);
        Assert.DoesNotContain("new Color(0.78f, 0.7f, 0.55f)", source);
        Assert.DoesNotContain("new Color(0.18f, 0.2f, 0.55f)", source);
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
