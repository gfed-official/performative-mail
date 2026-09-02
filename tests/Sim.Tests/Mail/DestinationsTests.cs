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
    public void TryDeliver_OneShiftLateLetter_MatchingHouseMailbox_PaysFourCents()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseOak, currentShift: 2, fx.Wallet);

        var delivered = Assert.IsType<Delivered>(result);
        Assert.Equal(new Cents(4), delivered.Paid);
        Assert.Equal(new Cents(4), fx.Wallet.Balance);
        Assert.False(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_TwoShiftsLateLetter_MatchingHouseMailbox_PaysZeroCents()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseOak, currentShift: 3, fx.Wallet);

        var delivered = Assert.IsType<Delivered>(result);
        Assert.Equal(new Cents(0), delivered.Paid);
        Assert.Equal(new Cents(0), fx.Wallet.Balance);
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
    public void TryDeliver_KindRejectWhileLate_DoesNotConsumeOrChangeWallet()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterCargo(fx.Oak);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseOak, currentShift: 3, fx.Wallet);

        Assert.Equal(RejectReason.KindNotAccepted, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(new Cents(0), fx.Wallet.Balance);
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_LetterAddressMismatch_DebitsFourAndConsumes()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseElm, currentShift: 1, fx.Wallet);

        var misdelivered = Assert.IsType<Misdelivered>(result);
        Assert.Equal(new Cents(4), misdelivered.Penalty);
        Assert.Equal(new Cents(-4), fx.Wallet.Balance);
        Assert.False(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_LetterAddressMismatchWhileLate_DebitsFourAndConsumes()
    {
        var fx = new DeliveryFixture();
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseElm, currentShift: 3, fx.Wallet);

        var misdelivered = Assert.IsType<Misdelivered>(result);
        Assert.Equal(new Cents(4), misdelivered.Penalty);
        Assert.Equal(new Cents(-4), fx.Wallet.Balance);
        Assert.False(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_LetterAddressMismatchAtWalletFloor_RejectsAndKeepsItem()
    {
        var fx = new DeliveryFixture(new Cents(-500));
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseElm, currentShift: 1, fx.Wallet);

        Assert.Equal(RejectReason.WalletFloor, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(new Cents(-500), fx.Wallet.Balance);
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_LetterAddressMismatchFromNegative497_RejectsAndKeepsItem()
    {
        var fx = new DeliveryFixture(new Cents(-497));
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseElm, currentShift: 1, fx.Wallet);

        Assert.Equal(RejectReason.WalletFloor, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(new Cents(-497), fx.Wallet.Balance);
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void TryDeliver_LetterAddressMismatchFromNegative496_LandsOnWalletFloor()
    {
        var fx = new DeliveryFixture(new Cents(-496));
        var mailId = fx.RegisterLetter(fx.Oak, value: MailKinds.LetterBaseValue, deadlineShift: 1);

        var result = fx.Destinations.TryDeliver(mailId, fx.HouseElm, currentShift: 1, fx.Wallet);

        var misdelivered = Assert.IsType<Misdelivered>(result);
        Assert.Equal(new Cents(4), misdelivered.Penalty);
        Assert.Equal(new Cents(-500), fx.Wallet.Balance);
        Assert.False(fx.Mail.Contains(mailId));
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

    public DeliveryFixture(Cents wallet = default)
    {
        Mail = new MailRegistry();
        Destinations = new Destinations(Mail);
        Wallet = new Wallet(wallet);
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
