using System.Text.RegularExpressions;

namespace PerformativeMail.Net.Tests.UI;

public sealed class HudSceneTests
{
    private static readonly Regex NodeHeader = new(
        @"^\[node name=""([^""]+)"" type=""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void LayoutNodes_IgnoreMouse_SoLobbyClicksPass()
    {
        var text = File.ReadAllText(FindHudScene());
        var blocks = NodeBlocks(text);
        Assert.NotEmpty(blocks);

        var blockers = blocks
            .Where(block => !HasIgnoreMouse(block.Body))
            .Select(block => block.Name + " (" + block.Type + ")")
            .ToArray();

        Assert.True(
            blockers.Length == 0,
            "HUD layout nodes default to Stop and sit on CanvasLayer 10 over Host/Join. " +
            "Set mouse_filter = 2 (Ignore) on every node. Missing: " +
            string.Join(", ", blockers));
    }

    private static IReadOnlyList<(string Name, string Type, string Body)> NodeBlocks(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<(string Name, string Type, string Body)>();
        string? name = null;
        string? type = null;
        var body = new List<string>();

        void Flush()
        {
            if (name is null || type is null)
                return;
            blocks.Add((name, type, string.Join('\n', body)));
        }

        foreach (var line in lines)
        {
            var match = NodeHeader.Match(line);
            if (match.Success)
            {
                Flush();
                name = match.Groups[1].Value;
                type = match.Groups[2].Value;
                body.Clear();
                continue;
            }

            if (name is not null)
                body.Add(line);
        }

        Flush();
        return blocks;
    }

    private static bool HasIgnoreMouse(string body)
    {
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
                break;
            if (line == "mouse_filter = 2")
                return true;
        }

        return false;
    }

    private static string FindHudScene()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", "scenes", "hud.tscn");
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("game/scenes/hud.tscn");
    }
}
