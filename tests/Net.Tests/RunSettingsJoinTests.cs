using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class RunSettingsJoinTests
{
    [Fact]
    public void DefaultServerRuntime_OffersArcade_AfterHello()
    {
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A), new SimWorld());
        var client = new ClientRuntime();
        client.Connect(loopback.B);

        server.TickOnce();
        client.Receive();

        Assert.Equal(RunSettings.Arcade(), server.OfferedSettings);
        Assert.Equal(server.OfferedSettings, client.AcceptedSettings);
        Assert.Equal(RunSettings.Arcade(), client.AcceptedSettings);
        Assert.NotNull(client.LocalPlayer);
        Assert.Null(client.LastReject);
    }

    [Fact]
    public void CustomSettings_RoundTripIdentically()
    {
        var settings = Custom();
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(loopback.A),
            new SimWorld(),
            offeredWorld: null,
            settings);
        var client = new ClientRuntime();
        client.Connect(loopback.B);

        server.TickOnce();
        client.Receive();

        Assert.Equal(settings, server.OfferedSettings);
        Assert.Equal(settings, client.AcceptedSettings);
        Assert.NotNull(client.LocalPlayer);
        Assert.Null(client.LastReject);
    }

    [Fact]
    public void SettingsThenMatchingWorldOffer_BothArrive()
    {
        var settings = Custom();
        ulong hash = WorldHash.Compute(WorldGen.GenerateSmallIsland(settings.Seed));
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(
            LoopbackLink.OverPipes(loopback.A),
            new SimWorld(),
            new WorldOffer(settings.Seed, hash),
            settings);
        var client = new ClientRuntime();
        client.Connect(loopback.B);

        server.TickOnce();
        client.Receive();

        Assert.Equal(settings, client.AcceptedSettings);
        Assert.Equal(hash, client.AcceptedWorldHash);
        Assert.NotNull(client.LocalPlayer);
        Assert.Null(client.LastReject);
    }

    private static RunSettings Custom() => new(
        2134567890,
        "small_island",
        new[] { "double_raids" },
        4,
        LobbyVisibility.Invite,
        "land",
        Protocol.SchemaHash,
        Protocol.ContentHash);
}
