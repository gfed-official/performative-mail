namespace PerformativeMail.Net.Tests.UI;

public sealed class InputSamplerSourceTests
{
    [Fact]
    public void SamplesHotbarKeysAndWheel()
    {
        string source = ReadGame("InputSampler.cs");
        Assert.Contains("HotbarSlots = 8", source);
        Assert.Contains("DefaultHotbarSlot = 1", source);
        Assert.Contains("TryHotbarSlot", source);
        Assert.Contains("TryHotbarWheel", source);
        Assert.Contains("Key.Key1 or Key.Kp1", source);
        Assert.Contains("Key.Key8 or Key.Kp8", source);
        Assert.Contains("MouseButton.WheelUp", source);
        Assert.Contains("MouseButton.WheelDown", source);
        Assert.Contains("WrapHotbarSlot", source);
        Assert.Contains("MapHeld", source);
        Assert.Contains("Key.M", source);
        Assert.Contains("JoyButton.Back", source);
        Assert.Contains("BuildHeld", source);
        Assert.Contains("Key.B", source);
        Assert.Contains("JoyButton.LeftShoulder", source);
        Assert.Contains("RotateHeld", source);
        Assert.Contains("Key.R", source);
        Assert.Contains("PipetteHeld", source);
        Assert.Contains("Key.Q", source);
        Assert.Contains("PlaceHeld", source);
        Assert.Contains("MouseButton.Left", source);
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
