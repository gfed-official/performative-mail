namespace PerformativeMail.Net.Tests.UI;

public sealed class PawnStageBootTests
{
    [Fact]
    public void Spawn_AttachesCameraAtEyeHeight()
    {
        var source = ReadPawnStage();
        Assert.Contains("CameraName = \"Camera\"", source);
        Assert.Contains("FirstPersonLook.EyeHeightMeters", source);
        Assert.Contains("PawnLabelPlacement.AbovePawn", source);
        Assert.Contains("PawnPalette.Rgb", source);
        Assert.Contains("ArtMesh.PawnRemote", source);
        Assert.Contains("ArtMesh.ApplyPawnKitColor", source);
        Assert.Contains("ArtMesh.PathForMail", source);
        Assert.Contains("HeldMailName", source);
        Assert.Contains("ViewFrame.From", source);
        Assert.Contains("camera.Current = local", source);
        Assert.Contains("localPitchRadians", source);
        Assert.Contains("OutlineSize = LabelOutlineSize", source);
        Assert.Contains("PixelSize = LabelPixelSize", source);
        Assert.Contains("LabelOutlineSize = 8", source);
        Assert.Contains("LabelPixelSize = 0.01f", source);
        Assert.Contains("Modulate = Colors.White", source);
        Assert.Contains("Billboard = BaseMaterial3D.BillboardModeEnum.Enabled", source);
        Assert.DoesNotContain("TryLocalEye", source);
        Assert.DoesNotContain("QuadMesh", source);
    }

    private static string ReadPawnStage()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", "PawnStage.cs");
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("game/PawnStage.cs");
    }
}
