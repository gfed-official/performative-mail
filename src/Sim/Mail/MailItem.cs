using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Mail;

public readonly record struct MailItem(
    MailId Id,
    MailKindId Kind,
    AddressId Address,
    ushort Value,
    byte SpawnShift,
    byte DeadlineShift,
    byte Flags = 0);

public static class MailKinds
{
    public static readonly MailKindId Letter = new(1);
    public static readonly MailKindId Postcard = new(2);
    public static readonly MailKindId SmallPackage = new(3);
    public static readonly MailKindId MediumPackage = new(4);
    public static readonly MailKindId LargePackage = new(5);
    public static readonly MailKindId Cargo = new(6);

    public const ushort LetterBaseValue = 8;

    public static bool Accepts(DestinationType type, MailKindId kind)
    {
        switch (type)
        {
            case DestinationType.HouseMailbox:
            case DestinationType.ApartmentMailRoom:
                return IsKnownKind(kind) && !kind.Equals(Cargo);
            case DestinationType.POBoxBank:
                return kind.Equals(Postcard) || kind.Equals(Letter);
            case DestinationType.BusinessDock:
                return kind.Equals(Cargo);
            default:
                throw Unexpected(type);
        }
    }

    private static bool IsKnownKind(MailKindId kind)
        => kind.Equals(Letter)
        || kind.Equals(Postcard)
        || kind.Equals(SmallPackage)
        || kind.Equals(MediumPackage)
        || kind.Equals(LargePackage)
        || kind.Equals(Cargo);

    private static Exception Unexpected(DestinationType type)
        => new ArgumentOutOfRangeException(nameof(type), type, null);
}

public sealed class MailRegistry
{
    private readonly Dictionary<MailId, MailItem> _items = new();

    public bool Register(MailItem item)
    {
        if (_items.ContainsKey(item.Id))
            return false;
        _items[item.Id] = item;
        return true;
    }

    public bool Contains(MailId id) => _items.ContainsKey(id);

    public bool TryGet(MailId id, out MailItem item) => _items.TryGetValue(id, out item);

    public bool Remove(MailId id) => _items.Remove(id);
}
