using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class WorldHashMismatchTests
{
    private const uint FixedSeed = 0x7F3A9C21;
    private const ulong GoldenWorldHash = 0x821670054873680EUL;

    [Fact]
    public void MatchingWorldOffer_ClientRegenEqualsServerHash()
    {
        ulong hash = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(loopback.A),
            new SimWorld(),
            new WorldOffer(FixedSeed, hash));
        var client = new ClientRuntime();
        client.Connect(loopback.B);

        server.TickOnce();
        client.Receive();

        Assert.Null(client.LastReject);
        Assert.NotNull(client.GeneratedWorld);
        Assert.Equal(hash, client.AcceptedWorldHash);
        Assert.Equal(GoldenWorldHash, hash);
        Assert.Equal(hash, WorldHash.Compute(client.GeneratedWorld));
    }

    [Fact]
    public void ForcedHashMismatch_RejectsWithVersionMismatch()
    {
        ulong hash = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(loopback.A),
            new SimWorld(),
            new WorldOffer(FixedSeed, hash ^ 1UL));
        var client = new ClientRuntime();
        client.Connect(loopback.B);

        server.TickOnce();
        client.Receive();

        Assert.NotNull(client.LastReject);
        Assert.Equal(HelloRejectReason.VersionMismatch, client.LastReject.Value.Reason);
        Assert.Null(client.GeneratedWorld);
        Assert.Null(client.AcceptedWorldHash);
    }
}
