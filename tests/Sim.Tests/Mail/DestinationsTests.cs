using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Sim.Tests.Mail;

public sealed class DestinationsTests
{
    [Fact]
    public void TryDeliver_OnTimeLetter_MatchingHouseMailbox_PaysEightCents()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseOak, currentShift: 1, fx.Wallet);

        var delivered = Assert.IsType<Delivered>(result);
        Assert.Equal(new Cents(8), delivered.Paid);
        Assert.Equal(new Cents(8), fx.Wallet.Balance);
        Assert.False(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_CargoIntoHouseMailbox_RejectsKindNotAccepted()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterCargo(fx.Oak);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseOak, currentShift: 1, fx.Wallet);

        Assert.Equal(RejectReason.KindNotAccepted, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(new Cents(0), fx.Wallet.Balance);
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_LetterAddressMismatch_ThrowsU33Misdelivery()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var ex = Assert.Throws<InvalidOperationException>(
            () => fx.Destinations.TryDeliver(mailId, fx.HouseElm, currentShift: 1, fx.Wallet));

        Assert.Equal("U3.3 misdelivery", ex.Message);
        Assert.Equal(new Cents(0), fx.Wallet.Balance);
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_UnknownMail_RejectsWithoutWalletChange()
    {
        var fx = new DeliveryFixture();

        var result = fx.Destinations.TryDeliver(new MailId(99), fx.HouseOak, currentShift: 1, fx.Wallet);

        Assert.Equal(RejectReason.UnknownMail, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(new Cents(0), fx.Wallet.Balance);
    }

    [Fact]
    public void TryDeliver_UnknownDestination_RejectsWithoutWalletChange()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, new DestinationId(99), currentShift: 1, fx.Wallet);

        Assert.Equal(RejectReason.UnknownDestination, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(new Cents(0), fx.Wallet.Balance);
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_ConsumedId_RejectsUnknownMail()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);
        Assert.IsType<Delivered>(fx.Destinations.TryDeliver(mailId, fx.HouseOak, currentShift: 1, fx.Wallet));

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseOak, currentShift: 1, fx.Wallet);

        Assert.Equal(RejectReason.UnknownMail, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(new Cents(8), fx.Wallet.Balance);
        Assert.False(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_CargoWrongAddress_RejectsKindNotAccepted()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterCargo(fx.Elm);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseOak, currentShift: 1, fx.Wallet);

        Assert.Equal(RejectReason.KindNotAccepted, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(new Cents(0), fx.Wallet.Balance);
        Assert.True(fx.Mail.Contains(mailId));
    }
}

internal sealed class DeliveryFixture
{
    private uint _nextMail = 1;

    public DeliveryFixture()
    {
        Mail = new MailRegistry();
        Destinations = new Destinations(Mail);
        Wallet = new Wallet();
        Oak = new AddressId(1, 4, 13, 0);
        Elm = new AddressId(1, 5, 2, 0);
        HouseOak = new DestinationId(1);
        HouseElm = new DestinationId(2);
        Assert.True(Destinations.Register(new Destination(HouseOak, DestinationType.HouseMailbox, Oak)));
        Assert.True(Destinations.Register(new Destination(HouseElm, DestinationType.HouseMailbox, Elm)));
    }

    public MailRegistry Mail { get; }

    public Destinations Destinations { get; }

    public Wallet Wallet { get; }

    public AddressId Oak { get; }

    public AddressId Elm { get; }

    public DestinationId HouseOak { get; }

    public DestinationId HouseElm { get; }

    public MailId RegisterLetter(AddressId address, ushort value, byte deadlineShift, byte spawnShift = 1)
    {
        var id = new MailId(_nextMail++);
        Assert.True(Mail.Register(new MailItem(id, MailKinds.Letter, address, value, spawnShift, deadlineShift)));
        return id;
    }

    public MailId RegisterCargo(AddressId address)
    {
        var id = new MailId(_nextMail++);
        Assert.True(Mail.Register(new MailItem(id, MailKinds.Cargo, address, 600, 1, 2)));
        return id;
    }
}
