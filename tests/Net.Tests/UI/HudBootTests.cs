using PerformativeMail.Client.UI;

namespace PerformativeMail.Net.Tests.UI;

public sealed class HudBootTests
{
    [Fact]
    public void Placeholder_IsTheGarbledLobbyChrome()
    {
        var frame = HudFrame.From(HudBoot.Placeholder());

        Assert.Equal("Shift 1 / 5", frame.ShiftLabel);
        Assert.Equal("DELIVERY", frame.PhaseLabel);
        Assert.Equal("13 Larch Lane", frame.HeldAddress);
        Assert.Equal("13 Larch Lane", frame.TargetAddress);
        Assert.Equal("tick", frame.MatchLabel);
        Assert.Equal("$18.20", frame.WalletLabel);
        Assert.Equal("Quota 640 / 2214", frame.QuotaLabel);
        Assert.Equal("", frame.SurplusLabel);
        Assert.Equal("23", frame.ComplaintLabel);
    }
}
