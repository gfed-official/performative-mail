using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class ComplaintMeterTests
{
    [Fact]
    public void AddMisdelivery_Letter_AddsFive()
    {
        var meter = new ComplaintMeter();
        meter.AddMisdelivery(MailKinds.Letter);
        Assert.Equal(5, meter.Points);
    }

    [Fact]
    public void ComplaintOnMisdelivery_Chapter11Table()
    {
        Assert.Equal(3, MailKinds.ComplaintOnMisdelivery(MailKinds.Postcard));
        Assert.Equal(5, MailKinds.ComplaintOnMisdelivery(MailKinds.Letter));
        Assert.Equal(8, MailKinds.ComplaintOnMisdelivery(MailKinds.SmallPackage));
        Assert.Equal(12, MailKinds.ComplaintOnMisdelivery(MailKinds.MediumPackage));
        Assert.Equal(16, MailKinds.ComplaintOnMisdelivery(MailKinds.LargePackage));
        Assert.Equal(20, MailKinds.ComplaintOnMisdelivery(MailKinds.Cargo));
    }

    [Fact]
    public void Add_ClampsToZeroAndOneHundred()
    {
        var high = new ComplaintMeter(98);
        high.AddMisdelivery(MailKinds.Letter);
        Assert.Equal(100, high.Points);

        var low = new ComplaintMeter(1);
        low.Decay(20, 0.1);
        Assert.Equal(0, low.Points);
    }

    [Fact]
    public void Decay_TenSecondsAtPointOne_DropsOne()
    {
        var meter = new ComplaintMeter(10);
        meter.Decay(10, 0.1);
        Assert.Equal(9, meter.Points);
    }

    [Fact]
    public void AddLateDelivery_AddsTwo()
    {
        var meter = new ComplaintMeter();
        meter.AddLateDelivery();
        Assert.Equal(2, meter.Points);
    }

    [Fact]
    public void AddBacklogTick_AddsOne()
    {
        var meter = new ComplaintMeter();
        meter.AddBacklogTick();
        Assert.Equal(1, meter.Points);
    }

    [Fact]
    public void InspectorDue_AtSeventyFive()
    {
        Assert.False(new ComplaintMeter(74).InspectorDue());
        Assert.True(new ComplaintMeter(75).InspectorDue());
    }

    [Fact]
    public void RaidMultiplier_IsOnePlusPointsOverOneHundred()
    {
        Assert.Equal(1.0, new ComplaintMeter(0).RaidMultiplier);
        Assert.Equal(1.5, new ComplaintMeter(50).RaidMultiplier);
        Assert.Equal(2.0, new ComplaintMeter(100).RaidMultiplier);
    }

    [Fact]
    public void Constructor_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComplaintMeter(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComplaintMeter(101));
    }
}
