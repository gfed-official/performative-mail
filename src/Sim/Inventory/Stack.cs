using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Inventory;

public readonly struct Amount
{
    private readonly int _count;

    private Amount(int count) => _count = count;

    public static Amount All => default;

    public static Amount Of(int n)
    {
        if (n < 1) throw new ArgumentOutOfRangeException(nameof(n));
        return new Amount(n);
    }

    public bool IsAll => _count == 0;

    public int Value => _count;

    public int Resolve(int available) => IsAll ? available : _count;
}

public enum StackCategory : byte { Mail, Tool, Material, Consumable, Ammo, Blueprint, Weapon }

public enum WeightClass : byte { Light = 1, Medium = 3, Heavy = 8, Bulk = 255 }

public readonly record struct StackKey
{
    public readonly bool IsMail;
    public readonly ushort Def;
    public readonly uint Address;

    private StackKey(bool isMail, ushort def, uint address)
    {
        IsMail = isMail;
        Def = def;
        Address = address;
    }

    public static StackKey Mail(MailKindId kind, AddressId address)
        => new(true, kind.Value, address.Packed);

    public static StackKey Item(ItemDefId item) => new(false, item.Value, 0);
}

public abstract record Stack
{
    public abstract StackKey Key { get; }

    public abstract int Count { get; }

    public abstract Stack Take(int n, out Stack? rest);

    public abstract Stack Merge(Stack other);
}

public sealed record MailStack : Stack
{
    public MailKindId Kind { get; }

    public AddressId Address { get; }

    public IReadOnlyList<MailId> Ids { get; }

    public MailStack(MailKindId kind, AddressId address, IReadOnlyList<MailId> ids)
    {
        if (ids is null) throw new ArgumentNullException(nameof(ids));
        if (ids.Count == 0) throw new ArgumentException("A stack must contain at least one mail id.", nameof(ids));
        var copy = new MailId[ids.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = ids[i];
        Kind = kind;
        Address = address;
        Ids = copy;
    }

    public static MailStack Single(MailKindId kind, AddressId address, MailId id)
        => new(kind, address, new[] { id });

    public override StackKey Key => StackKey.Mail(Kind, Address);

    public override int Count => Ids.Count;

    public override Stack Take(int n, out Stack? rest)
    {
        if (n < 1 || n > Count) throw new ArgumentOutOfRangeException(nameof(n));
        var src = (MailId[])Ids;
        if (n == Count)
        {
            rest = null;
            return this;
        }

        var taken = new MailId[n];
        Array.Copy(src, src.Length - n, taken, 0, n);
        var kept = new MailId[src.Length - n];
        Array.Copy(src, 0, kept, 0, kept.Length);
        rest = new MailStack(Kind, Address, kept);
        return new MailStack(Kind, Address, taken);
    }

    public override Stack Merge(Stack other)
    {
        if (other is not MailStack mail || !mail.Key.Equals(Key))
            throw new ArgumentException("Stacks merge only when they share a key.", nameof(other));
        var combined = new MailId[Count + mail.Count];
        Array.Copy((MailId[])Ids, 0, combined, 0, Count);
        Array.Copy((MailId[])mail.Ids, 0, combined, Count, mail.Count);
        return new MailStack(Kind, Address, combined);
    }
}

public sealed record ItemStack : Stack
{
    public ItemDefId Item { get; }

    public override int Count { get; }

    public ItemStack(ItemDefId item, int count)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        Item = item;
        Count = count;
    }

    public override StackKey Key => StackKey.Item(Item);

    public override Stack Take(int n, out Stack? rest)
    {
        if (n < 1 || n > Count) throw new ArgumentOutOfRangeException(nameof(n));
        if (n == Count)
        {
            rest = null;
            return this;
        }

        rest = new ItemStack(Item, Count - n);
        return new ItemStack(Item, n);
    }

    public override Stack Merge(Stack other)
    {
        if (other is not ItemStack item || !item.Key.Equals(Key))
            throw new ArgumentException("Stacks merge only when they share a key.", nameof(other));
        return new ItemStack(Item, Count + item.Count);
    }
}

public interface IStackCatalog
{
    Footprint FootprintOf(StackKey key);

    int MaxStackOf(StackKey key);

    WeightClass WeightOf(StackKey key);

    StackCategory CategoryOf(StackKey key);
}

public sealed record ContainerSpec(ContainerShape Shape, IReadOnlyCollection<StackCategory>? AllowedCategories)
{
    public bool Accepts(StackCategory category)
    {
        if (AllowedCategories is null) return true;
        foreach (var allowed in AllowedCategories)
            if (allowed == category) return true;
        return false;
    }

    public static ContainerSpec Chest => new(ContainerShape.Grid(8, 4), null);

    public static ContainerSpec DeathBag => new(ContainerShape.Grid(8, 4), null);

    public static ContainerSpec BaseInventory => new(ContainerShape.Grid(8, 2), null);

    public static ContainerSpec Backpack => new(ContainerShape.Grid(8, 2), null);

    public static ContainerSpec Hotbar => new(ContainerShape.Grid(8, 1, new Cell(0, 0)), null);

    public static ContainerSpec Cursor => new(ContainerShape.Slot, null);

    public static ContainerSpec Intake => new(ContainerShape.Grid(20, 16), new[] { StackCategory.Mail });

    public static ContainerSpec Depot => new(ContainerShape.Grid(20, 16), null);
}
