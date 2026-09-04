using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Mail;

public enum DestinationType : byte
{
    HouseMailbox,
    ApartmentMailRoom,
    POBoxBank,
    BusinessDock,
}

public readonly record struct Destination(
    DestinationId Id,
    DestinationType Type,
    AddressId Address);

public abstract record DeliverResult;

public sealed record Delivered(Cents Paid) : DeliverResult;

public sealed record Misdelivered(Cents Penalty) : DeliverResult;

public sealed record Rejected(RejectReason Reason) : DeliverResult;

public enum RejectReason : byte
{
    UnknownMail,
    UnknownDestination,
    KindNotAccepted,
    WalletFloor,
}

public sealed class Destinations
{
    private readonly MailRegistry _mail;
    private readonly Dictionary<DestinationId, Destination> _destinations = new();

    public Destinations(MailRegistry mail)
    {
        _mail = mail ?? throw new ArgumentNullException(nameof(mail));
    }

    public bool Register(Destination destination)
    {
        if (_destinations.ContainsKey(destination.Id))
            return false;
        _destinations[destination.Id] = destination;
        return true;
    }

    public bool TryGet(DestinationId id, out Destination destination)
        => _destinations.TryGetValue(id, out destination);

    public DeliverResult TryDeliver(
        MailId mailId,
        DestinationId destinationId,
        byte currentShift,
        Wallet wallet,
        ComplaintMeter? complaint = null)
    {
        if (wallet is null)
            throw new ArgumentNullException(nameof(wallet));

        if (!_mail.TryGet(mailId, out var item))
            return new Rejected(RejectReason.UnknownMail);

        if (!_destinations.TryGetValue(destinationId, out var destination))
            return new Rejected(RejectReason.UnknownDestination);

        if (!MailKinds.Accepts(destination.Type, item.Kind))
            return new Rejected(RejectReason.KindNotAccepted);

        if (!item.Address.Equals(destination.Address))
            return TryMisdeliver(mailId, item, wallet, complaint);

        _mail.Remove(mailId);
        var paid = PayForTimeliness(item.Value, currentShift, item.DeadlineShift);
        wallet.Credit(paid);
        if (currentShift > item.DeadlineShift)
            complaint?.AddLateDelivery();
        return new Delivered(paid);
    }

    // §2.2 rule 5: consume and debit misdeliveryPenaltyRatio × value (0.5) when
    // TryDebit succeeds. Reject WalletFloor and do not consume when the debit
    // would land below -500. Lateness does not change the penalty.
    private DeliverResult TryMisdeliver(MailId mailId, MailItem item, Wallet wallet, ComplaintMeter? complaint)
    {
        var penalty = PenaltyForMisdelivery(item.Value);
        if (!wallet.TryDebit(penalty))
            return new Rejected(RejectReason.WalletFloor);
        _mail.Remove(mailId);
        complaint?.AddMisdelivery(item.Kind);
        return new Misdelivered(penalty);
    }

    private static Cents PayForTimeliness(ushort value, byte currentShift, byte deadlineShift)
    {
        int lateBy = currentShift - deadlineShift;
        if (lateBy <= 0)
            return new Cents(value);
        if (lateBy < MailSpawnConstants.DeadLetterShifts)
        {
            int paid = checked((int)Math.Round(
                value * MailSpawnConstants.LateValueRatio,
                MidpointRounding.AwayFromZero));
            return new Cents(paid);
        }

        return new Cents(0);
    }

    // §2.2 rule 5 / chapter 11: misdeliveryPenaltyRatio 0.5.
    private static Cents PenaltyForMisdelivery(ushort value)
        => new Cents(value / 2);
}
