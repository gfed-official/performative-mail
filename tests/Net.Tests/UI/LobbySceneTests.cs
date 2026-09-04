using System.Text.RegularExpressions;

namespace PerformativeMail.Net.Tests.UI;

public sealed class LobbySceneTests
{
    private static readonly Regex NodeHeader = new(
        @"^\[node name=""([^""]+)"" type=""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] RequiredUnique = { "SeedLabel", "ArchetypeLabel", "KitDropdown", "ReadyLabel", "StartLabel", "PlayerList" };

    [Fact]
    public void LayoutNodes_IgnoreMouse_SoHostJoinClicksPass()
    {
        var text = File.ReadAllText(FindLobbyScene());
        var blocks = NodeBlocks(text);
        Assert.NotEmpty(blocks);

        var blockers = blocks
            .Where(block => !HasIgnoreMouse(block.Body))
            .Select(block => block.Name + " (" + block.Type + ")")
            .ToArray();

        Assert.True(
            blockers.Length == 0,
            "Lobby layout nodes default to Stop and sit over Host/Join. " +
            "Set mouse_filter = 2 (Ignore) on every node. Missing: " +
            string.Join(", ", blockers));
    }

    [Fact]
    public void Scene_HasSeedArchetypeKitReadyStartAndPlayerList()
    {
        var text = File.ReadAllText(FindLobbyScene());
        foreach (var name in RequiredUnique)
        {
            Assert.Contains($"name=\"{name}\"", text);
            Assert.Contains("unique_name_in_owner = true", NodeBody(text, name));
        }

        Assert.Contains("type=\"OptionButton\"", NodeHeaderLine(text, "KitDropdown"));
    }

    [Fact]
    public void Scene_HasNoStampGrid()
    {
        var text = File.ReadAllText(FindLobbyScene());
        Assert.DoesNotContain("Stamp", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scene_StartsHidden()
    {
        var root = NodeBody(File.ReadAllText(FindLobbyScene()), "Lobby");
        Assert.Contains("visible = false", root);
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

    private static string NodeBody(string text, string name)
    {
        var block = NodeBlocks(text).Single(b => b.Name == name);
        return block.Body;
    }

    private static string NodeHeaderLine(string text, string name)
    {
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var match = NodeHeader.Match(line);
            if (match.Success && match.Groups[1].Value == name)
                return line;
        }

        throw new InvalidOperationException("missing node " + name);
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

    private static string FindLobbyScene()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", "scenes", "lobby.tscn");
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("game/scenes/lobby.tscn");
    }
}
