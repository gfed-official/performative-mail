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
        Assert.Contains("mat_pawn_vest", source);
        Assert.Contains("mat_pawn_hat", source);
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
        Assert.True(File.Exists(Path.Combine(art, "props", "mail_letter.glb")));
        Assert.True(File.Exists(Path.Combine(art, "props", "mail_pkg_s.glb")));
        Assert.True(File.Exists(Path.Combine(art, "props", "mail_pkg_m.glb")));
        Assert.True(File.Exists(Path.Combine(art, "props", "mail_pkg_l.glb")));
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
}
