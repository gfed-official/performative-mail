using System.Diagnostics;
using PerformativeMail.Sim.World;
using Xunit.Abstractions;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldGenBudgetTests
{
    private readonly ITestOutputHelper _output;

    public WorldGenBudgetTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void GenerateSmallIsland_CompletesWithinThreeSeconds()
    {
        WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);

        var clock = Stopwatch.StartNew();
        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        clock.Stop();

        _output.WriteLine($"Small Island generation {clock.ElapsedMilliseconds} ms");
        Assert.True(tables.Valid);
        Assert.Equal(WorldGenHashTests.GoldenWorldHash, WorldHash.Compute(tables));
        Assert.True(
            clock.ElapsedMilliseconds <= 3000,
            $"generation took {clock.ElapsedMilliseconds} ms");
    }
}
