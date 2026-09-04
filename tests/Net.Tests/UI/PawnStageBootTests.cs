namespace PerformativeMail.Net.Tests.UI;

public sealed class PawnStageBootTests
{
    [Fact]
    public void Spawn_AttachesCameraAtEyeHeight()
    {
        var source = ReadPawnStage();
        Assert.Contains("CameraName = \"Camera\"", source);
        Assert.Contains("FirstPersonLook.EyeHeightMeters", source);
        Assert.Contains("camera.Current = local", source);
        Assert.Contains("localPitchRadians", source);
        Assert.DoesNotContain("TryLocalEye", source);
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
