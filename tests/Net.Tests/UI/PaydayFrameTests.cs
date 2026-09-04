using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Net.Tests.UI;

public sealed class PaydayFrameTests
{
    [Fact]
    public void From_Boot_PrintsEarnedAndQuota()
    {
        var frame = PaydayFrame.From(PhaseOverlayBoot.Payday());

        Assert.Equal("640", frame.EarnedLabel);
        Assert.Equal("2214", frame.QuotaLabel);
    }

    [Fact]
    public void From_Zero_PrintsZeros()
    {
        var frame = PaydayFrame.From(new PaydaySnapshot(new Cents(0), new Cents(0)));

        Assert.Equal("0", frame.EarnedLabel);
        Assert.Equal("0", frame.QuotaLabel);
    }
}
