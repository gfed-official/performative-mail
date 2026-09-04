using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class DebugBootTests
{
    [Fact]
    public void Placeholder_IsPlayingHostWithArcadeSeed()
    {
        var snap = DebugBoot.Placeholder();
        var frame = DebugFrame.From(in snap);

        Assert.Equal(DebugConnection.Playing, snap.Connection);
        Assert.True(snap.Host);
        Assert.True(snap.CanCheat);
        Assert.Equal(RunPhase.Delivery, snap.Phase);
        Assert.Equal(RunSettings.Arcade().Seed, snap.Seed);
        Assert.Equal("PLAYING", frame.ConnectionLabel);
        Assert.Equal("$18.20", frame.WalletLabel);
        Assert.Equal(DebugFrame.HostAuthority, frame.AuthorityLabel);
    }
}
