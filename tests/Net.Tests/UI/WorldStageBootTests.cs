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
        Assert.Contains("Sync(WorldTables", source);
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
