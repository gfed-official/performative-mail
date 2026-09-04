using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class LobbyFrameTests
{
    [Fact]
    public void From_ArcadeNotReady_SeedArchetypeKitAndNotReady()
    {
        var frame = LobbyFrame.From(LobbyBoot.Arcade());

        Assert.Equal("0x7F3A9C21", frame.SeedLabel);
        Assert.Equal("small_island", frame.ArchetypeLabel);
        Assert.Equal("land", frame.KitLabel);
        Assert.Equal(LobbyFrame.NotReadyText, frame.ReadyLabel);
        Assert.Equal(LobbyFrame.StartText, frame.StartLabel);
        Assert.False(frame.StartEnabled);
        Assert.Equal("Jules host land not ready", frame.PlayerList);
    }

    [Fact]
    public void From_ArcadeReady_ReadyAndStartEnabled()
    {
        var frame = LobbyFrame.From(LobbyBoot.ArcadeReady());

        Assert.Equal("0x7F3A9C21", frame.SeedLabel);
        Assert.Equal("land", frame.KitLabel);
        Assert.Equal(LobbyFrame.ReadyText, frame.ReadyLabel);
        Assert.True(frame.StartEnabled);
        Assert.Equal("Jules host land ready", frame.PlayerList);
    }

    [Fact]
    public void From_EmptyPlayers_NotReadyStartDisabled()
    {
        var settings = RunSettings.Arcade();
        var frame = LobbyFrame.From(new LobbySnapshot(in settings, Array.Empty<LobbyPlayer>()));

        Assert.Equal(LobbyFrame.NotReadyText, frame.ReadyLabel);
        Assert.False(frame.StartEnabled);
        Assert.Equal("", frame.PlayerList);
    }

    [Fact]
    public void From_GuestNotReady_KeepsStartDisabled()
    {
        var settings = RunSettings.Arcade();
        var frame = LobbyFrame.From(new LobbySnapshot(
            in settings,
            new[]
            {
                new LobbyPlayer("Jules", "land", Ready: true, Host: true),
                new LobbyPlayer("Kim", "land", Ready: false, Host: false),
            }));

        Assert.Equal(LobbyFrame.NotReadyText, frame.ReadyLabel);
        Assert.False(frame.StartEnabled);
        Assert.Equal("Jules host land ready\nKim guest land not ready", frame.PlayerList);
    }

    [Fact]
    public void FormatSeed_UsesHexPrefix()
    {
        Assert.Equal("0x7F3A9C21", LobbyFrame.FormatSeed(RunSettings.Arcade().Seed));
        Assert.Equal("0x00000000", LobbyFrame.FormatSeed(0));
    }
}
