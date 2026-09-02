using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

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

public sealed record Rejected(RejectReason Reason) : DeliverResult;

public enum RejectReason : byte
{
    UnknownMail,
    UnknownDestination,
    KindNotAccepted,
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

    public DeliverResult TryDeliver(MailId mailId, DestinationId destinationId, byte currentShift, Wallet wallet)
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
            throw new InvalidOperationException("U3.3 misdelivery");

        if (currentShift > item.DeadlineShift)
            throw new InvalidOperationException("U3.2 late pay");

        _mail.Remove(mailId);
        var paid = new Cents(item.Value);
        wallet.Credit(paid);
        return new Delivered(paid);
    }
}
