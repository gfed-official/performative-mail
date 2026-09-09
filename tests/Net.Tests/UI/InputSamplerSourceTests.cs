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
