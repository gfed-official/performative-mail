using System.Text.Json;
using PerformativeMail.Sim.Meta;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Tests.Meta;

public sealed class MetaProfileStoreTests
{
    private static readonly StampScore CursedMail = new("cursed_mail", 1.15);
    private static readonly StampScore DoubleRaids = new("double_raids", 1.25);

    [Fact]
    public void Save_ThenLoad_RankXpMatches()
    {
        string path = TempProfile();
        var profile = new MetaProfile("local:jules", "Jules", 3400, ProfileUnlocks.RankOne());

        MetaProfileStore.Save(path, profile);
        var loaded = MetaProfileStore.Load(path);

        Assert.Equal(3400, loaded.RankXp);
        Assert.Equal(profile, loaded);
    }

    [Fact]
    public void WriteResults_ThenLoad_AddsPostalRankXp()
    {
        string path = TempProfile();
        var start = new MetaProfile("local:jules", "Jules", 3400, ProfileUnlocks.RankOne());
        var payload = ResultsPayload.From(
            true,
            5,
            20,
            10000,
            "small_island",
            0x7F3A9C21,
            new[] { CursedMail, DoubleRaids });

        var written = MetaProfileStore.WriteResults(path, start, payload);
        var loaded = MetaProfileStore.Load(path);

        Assert.Equal(650, payload.PostalRankXp);
        Assert.Equal(4050, written.RankXp);
        Assert.Equal(4050, loaded.RankXp);
        Assert.Equal(written, loaded);
        Assert.Equal(start.Unlocks, loaded.Unlocks);
        Assert.Equal(start.ProfileId, loaded.ProfileId);
        Assert.Equal(start.DisplayName, loaded.DisplayName);
    }

    [Fact]
    public void Save_FileIsValidJson()
    {
        string path = TempProfile();
        var profile = new MetaProfile("steam:7656", "Jules", 3400, ProfileUnlocks.RankOne());

        MetaProfileStore.Save(path, profile);
        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal(3400, doc.RootElement.GetProperty("rankXp").GetInt32());
        Assert.Equal("steam:7656", doc.RootElement.GetProperty("profileId").GetString());
        Assert.Equal("Jules", doc.RootElement.GetProperty("displayName").GetString());
        Assert.True(doc.RootElement.TryGetProperty("unlocks", out var unlocks));
        Assert.Equal(JsonValueKind.Object, unlocks.ValueKind);
        Assert.False(doc.RootElement.TryGetProperty("cosmetics", out _));
        Assert.False(doc.RootElement.TryGetProperty("stats", out _));
        Assert.False(doc.RootElement.TryGetProperty("recentRuns", out _));
        Assert.False(doc.RootElement.TryGetProperty("settings", out _));
    }

    [Fact]
    public void Load_InvalidJson_Throws()
    {
        string path = TempProfile();
        File.WriteAllText(path, "{ not json");

        var ex = Assert.Throws<InvalidOperationException>(() => MetaProfileStore.Load(path));
        Assert.Contains("invalid JSON", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Award_DoesNotRecomputeFormula()
    {
        var start = new MetaProfile("local:jules", "Jules", 100, ProfileUnlocks.RankOne());
        var next = start.Award(650);

        Assert.Equal(750, next.RankXp);
        Assert.Equal(start.Unlocks, next.Unlocks);
    }

    [Fact]
    public void RankXp_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MetaProfile("local:jules", "Jules", -1, ProfileUnlocks.RankOne()));
        var profile = new MetaProfile("local:jules", "Jules", 0, ProfileUnlocks.RankOne());
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.Award(-1));
    }

    private static string TempProfile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pm-u72-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, MetaProfileStore.FileName);
    }
}
