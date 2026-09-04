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

    public const ushort PostcardBaseValue = 4;
    public const ushort LetterBaseValue = 8;
    public const ushort SmallPackageBaseValue = 30;
    public const ushort MediumPackageBaseValue = 70;
    public const ushort LargePackageBaseValue = 160;
    public const ushort CargoBaseValue = 600;

    public const int PostcardComplaint = 3;
    public const int LetterComplaint = 5;
    public const int SmallPackageComplaint = 8;
    public const int MediumPackageComplaint = 12;
    public const int LargePackageComplaint = 16;
    public const int CargoComplaint = 20;

    // chapter 03 §1.1
    public static ushort BaseValue(MailKindId kind)
    {
        if (kind.Equals(Postcard)) return PostcardBaseValue;
        if (kind.Equals(Letter)) return LetterBaseValue;
        if (kind.Equals(SmallPackage)) return SmallPackageBaseValue;
        if (kind.Equals(MediumPackage)) return MediumPackageBaseValue;
        if (kind.Equals(LargePackage)) return LargePackageBaseValue;
        if (kind.Equals(Cargo)) return CargoBaseValue;
        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
    }

    public static int ComplaintOnMisdelivery(MailKindId kind)
    {
        if (kind.Equals(Postcard)) return PostcardComplaint;
        if (kind.Equals(Letter)) return LetterComplaint;
        if (kind.Equals(SmallPackage)) return SmallPackageComplaint;
        if (kind.Equals(MediumPackage)) return MediumPackageComplaint;
        if (kind.Equals(LargePackage)) return LargePackageComplaint;
        if (kind.Equals(Cargo)) return CargoComplaint;
        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
    }

    public static byte DeadlineOffsetShifts(MailKindId kind)
    {
        if (kind.Equals(Cargo)) return 1;
        if (IsKnownKind(kind)) return 0;
        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
    }

    // chapter 03 §1.1: value = base × (1 + 0.25 × (district − 1)) × (1 + 0.1 × (shift − 1))
    public static ushort ValueAtSpawn(MailKindId kind, byte district, byte shift)
    {
        if (district < 1) throw new ArgumentOutOfRangeException(nameof(district));
        if (shift < 1) throw new ArgumentOutOfRangeException(nameof(shift));
        int distanceNumerator = 4 + (district - 1);
        int shiftNumerator = 10 + (shift - 1);
        return (ushort)(BaseValue(kind) * distanceNumerator / 4 * shiftNumerator / 10);
    }

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
    private uint _nextId = 1;

    public IEnumerable<MailItem> Items => _items.Values;

    public int Count => _items.Count;

    public MailId Allocate()
    {
        while (_items.ContainsKey(new MailId(_nextId)))
            _nextId++;
        return new MailId(_nextId++);
    }

    public bool Register(MailItem item)
    {
        if (_items.ContainsKey(item.Id))
            return false;
        _items[item.Id] = item;
        if (item.Id.Value >= _nextId)
            _nextId = item.Id.Value + 1;
        return true;
    }

    public bool Contains(MailId id) => _items.ContainsKey(id);

    public bool TryGet(MailId id, out MailItem item) => _items.TryGetValue(id, out item);

    public bool Remove(MailId id) => _items.Remove(id);
}
