namespace PerformativeMail.Net.Tests.UI;

public sealed class PauseMenuSourceTests
{
    [Fact]
    public void Pressed_DefersPick_SoBindDoesNotFreeTheEmittingButton()
    {
        string pause = ReadGame("PauseMenu.cs");
        Assert.Contains("Callable.From(() => ChoicePicked?.Invoke(picked)).CallDeferred()", pause);
        Assert.DoesNotContain("button.Pressed += () => ChoicePicked?.Invoke(picked)", pause);
    }

    [Fact]
    public void ClearColumn_RemovesThenQueueFreesFromFront()
    {
        string pause = ReadGame("PauseMenu.cs");
        Assert.Contains("while (column.GetChildCount() > 0)", pause);
        Assert.Contains("column.RemoveChild(child)", pause);
        Assert.Contains("child.QueueFree()", pause);
        Assert.DoesNotContain("column.GetChild(0).Free()", pause);
        Assert.DoesNotContain("foreach (var child in column.GetChildren())", pause);
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
