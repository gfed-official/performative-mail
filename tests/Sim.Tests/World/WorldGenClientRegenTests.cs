using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldGenClientRegenTests
{
    public const uint FixedSeed = WorldGenHashTests.FixedSeed;

    [Fact]
    public void Regenerate_SameSeed_MatchesServerWorldHash()
    {
        var server = WorldGen.GenerateSmallIsland(FixedSeed);
        ulong serverHash = WorldHash.Compute(server);
        var client = WorldHashCheck.Regenerate(FixedSeed);
        ulong clientHash = WorldHash.Compute(client);

        Assert.Equal(WorldGenHashTests.GoldenWorldHash, serverHash);
        Assert.Equal(serverHash, clientHash);
        Assert.Equal(WorldHashVerdict.Match, WorldHashCheck.Compare(clientHash, serverHash));
    }

    [Fact]
    public void Accept_MatchingHash_ReturnsTables()
    {
        var server = WorldGen.GenerateSmallIsland(FixedSeed);
        ulong expected = WorldHash.Compute(server);
        var verdict = WorldHashCheck.Accept(FixedSeed, expected, out var tables, out ulong local);
        Assert.Equal(WorldHashVerdict.Match, verdict);
        Assert.Equal(expected, local);
        Assert.Equal(expected, WorldHash.Compute(tables));
        Assert.True(tables.Valid);
    }

    [Fact]
    public void Accept_MismatchedHash_IsVersionMismatch()
    {
        var server = WorldGen.GenerateSmallIsland(FixedSeed);
        ulong expected = WorldHash.Compute(server) ^ 1UL;
        var verdict = WorldHashCheck.Accept(FixedSeed, expected, out _, out ulong local);
        Assert.Equal(WorldGenHashTests.GoldenWorldHash, local);
        Assert.Equal(WorldHashVerdict.VersionMismatch, verdict);
    }

    [Fact]
    public void SeedView_FixedSeed_PrintsDistrictsAndAddresses()
    {
        var tables = WorldHashCheck.Regenerate(FixedSeed);
        ulong hash = WorldHash.Compute(tables);
        string view = SeedView.Render(FixedSeed, tables, hash);

        Assert.Contains("seed 0x7F3A9C21", view);
        Assert.Contains("worldHash 0x821670054873680E", view);
        Assert.Contains("valid true", view);
        Assert.Contains("districts ", view);
        Assert.Contains("streets", view);
        Assert.Contains("addresses", view);
        Assert.Contains("map", view);
        Assert.NotEmpty(tables.Addresses);
        var first = tables.Addresses[0];
        Assert.Contains($"{first.District}:{first.Street}:{first.Number}", view);
    }
}
