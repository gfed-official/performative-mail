using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class LobbyBootTests
{
    [Fact]
    public void Arcade_IsRunSettingsArcadeAndHostNotReady()
    {
        var snap = LobbyBoot.Arcade();

        Assert.Equal(RunSettings.Arcade(), snap.Settings);
        Assert.Equal(Array.Empty<string>(), snap.Settings.Stamps);
        Assert.Single(snap.Players);
        Assert.Equal(LobbyBoot.HostName, snap.Players[0].Name);
        Assert.Equal("land", snap.Players[0].Kit);
        Assert.True(snap.Players[0].Host);
        Assert.False(snap.Players[0].Ready);
    }

    [Fact]
    public void ArcadeReady_IsReady()
    {
        var snap = LobbyBoot.ArcadeReady();

        Assert.Equal(RunSettings.Arcade(), snap.Settings);
        Assert.True(snap.Players[0].Ready);
    }
}
