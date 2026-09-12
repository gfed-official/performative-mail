namespace PerformativeMail.Net.Tests.UI;

public sealed class ArtMeshBootTests
{
    [Fact]
    public void ArtMesh_WiresP1GlbPathsAndKitSlots()
    {
        var source = ReadGame("ArtMesh.cs");
        Assert.Contains("res://art/world/mailbox_01.glb", source);
        Assert.Contains("res://art/world/intake_01.glb", source);
        Assert.Contains("res://art/world/po_01.glb", source);
        Assert.Contains("res://art/world/house_a.glb", source);
        Assert.Contains("res://art/world/house_b.glb", source);
        Assert.Contains("res://art/world/house_c.glb", source);
        Assert.Contains("res://art/pawns/pawn_remote.glb", source);
        Assert.Contains("res://art/props/mail_letter.glb", source);
        Assert.Contains("res://art/props/mail_pkg_s.glb", source);
        Assert.Contains("res://art/props/mail_pkg_m.glb", source);
        Assert.Contains("res://art/props/mail_pkg_l.glb", source);
        Assert.Contains("res://art/world/street_tile_01.glb", source);
        Assert.Contains("res://art/world/street_curb_01.glb", source);
        Assert.Contains("res://art/world/spawn_pad_01.glb", source);
        Assert.Contains("res://art/world/grass_tile_01.glb", source);
        Assert.Contains("res://art/props/crate_01.glb", source);
        Assert.Contains("res://art/props/cart_01.glb", source);
        Assert.Contains("res://art/props/bike_01.glb", source);
        Assert.Contains("res://art/props/truck_01.glb", source);
        Assert.Contains("res://art/world/resource_wood_01.glb", source);
        Assert.Contains("res://art/world/resource_wood_stump_01.glb", source);
        Assert.Contains("res://art/world/resource_fiber_01.glb", source);
        Assert.Contains("res://art/world/resource_stone_01.glb", source);
        Assert.Contains("res://art/world/resource_iron_ore_01.glb", source);
        Assert.Contains("res://art/world/resource_sand_01.glb", source);
        Assert.Contains("res://art/world/resource_berries_01.glb", source);
        Assert.Contains("TryMesh", source);
        Assert.Contains("TryInstantiateLod", source);
        Assert.Contains("ApplyLod", source);
        Assert.Contains("LodRootName", source);
        Assert.Contains("ArtLod.MaxLevel", source);
        Assert.Contains("ArtLod.Resolve", source);
        Assert.Contains("ArtLod.Fallback", source);
        Assert.Contains("ArtLod.HorizontalMeters", source);
        Assert.Contains("PathForProp", source);
        Assert.Contains("PathForVehicle", source);
        Assert.Contains("VehicleArt.PathForVehicle", source);
        Assert.Contains("PathForResource", source);
        Assert.Contains("mat_pawn_vest", source);
        Assert.Contains("mat_pawn_hat", source);
        Assert.Contains("mat_district", source);
        Assert.Contains("ApplyDistrictColor", source);
        Assert.Contains("GltfDocument", source);
        Assert.Contains("ResourceLoader.Load", source);
        Assert.Contains("Art mesh missing:", source);
        Assert.Contains("PathForMail", source);
        Assert.Contains("MailKinds.SmallPackage", source);
        Assert.Contains("MailLetter", source);
    }

    [Fact]
    public void WorldStage_FallsBackToBoxesWithoutDoubleFlag()
    {
        var source = ReadGame("WorldStage.cs");
        Assert.Contains("AddMailboxFlag", source);
        Assert.Contains("AddHouseRoof", source);
        Assert.Contains("ArtMesh.Mailbox", source);
        Assert.DoesNotContain("AddMailboxFlag(root, size,", source);
    }

    [Fact]
    public void P1Glbs_ArePresentOnDisk()
    {
        string art = FindArtRoot();
        Assert.True(File.Exists(Path.Combine(art, "world", "mailbox_01.glb")));
        Assert.True(File.Exists(Path.Combine(art, "world", "intake_01.glb")));
        Assert.True(File.Exists(Path.Combine(art, "world", "po_01.glb")));
        Assert.True(File.Exists(Path.Combine(art, "world", "house_a.glb")));
        Assert.True(File.Exists(Path.Combine(art, "world", "house_b.glb")));
        Assert.True(File.Exists(Path.Combine(art, "world", "house_c.glb")));
        Assert.True(File.Exists(Path.Combine(art, "pawns", "pawn_remote.glb")));
        AssertNonEmptyGlb(Path.Combine(art, "props", "mail_letter.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "props", "mail_pkg_s.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "props", "mail_pkg_m.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "props", "mail_pkg_l.glb"));
        Assert.True(File.Exists(Path.Combine(art, "world", "street_tile_01.glb")));
        Assert.True(File.Exists(Path.Combine(art, "world", "street_curb_01.glb")));
        Assert.True(File.Exists(Path.Combine(art, "world", "spawn_pad_01.glb")));
        Assert.True(File.Exists(Path.Combine(art, "world", "grass_tile_01.glb")));
        Assert.True(File.Exists(Path.Combine(art, "props", "crate_01.glb")));
        Assert.True(File.Exists(Path.Combine(art, "props", "cart_01.glb")));
        AssertNonEmptyGlb(Path.Combine(art, "props", "bike_01.glb"));
    }

    [Fact]
    public void P4LodGlbs_ArePresentOnDiskWithoutCrateLo2()
    {
        string art = FindArtRoot();
        AssertNonEmptyGlb(Path.Combine(art, "world", "mailbox_01_lo1.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "mailbox_01_lo2.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "intake_01_lo1.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "intake_01_lo2.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "po_01_lo1.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "po_01_lo2.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "house_a_lo1.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "house_a_lo2.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "house_b_lo1.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "house_b_lo2.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "house_c_lo1.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "house_c_lo2.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "street_pole_01.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "street_pole_01_lo1.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "world", "street_pole_01_lo2.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "props", "cart_01_lo1.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "props", "cart_01_lo2.glb"));
        AssertNonEmptyGlb(Path.Combine(art, "props", "crate_01_lo1.glb"));
        Assert.False(File.Exists(Path.Combine(art, "props", "crate_01_lo2.glb")));
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

    private static string FindArtRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", "art");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException("game/art");
    }

    private static void AssertNonEmptyGlb(string path)
    {
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0, path);
    }
}
