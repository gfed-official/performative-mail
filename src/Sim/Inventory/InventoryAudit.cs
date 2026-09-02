using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Inventory;

public sealed class InventoryAudit : IEquatable<InventoryAudit>
{
    private readonly Dictionary<MailId, int> _mail;
    private readonly Dictionary<ItemDefId, int> _items;

    private InventoryAudit(Dictionary<MailId, int> mail, Dictionary<ItemDefId, int> items)
    {
        _mail = mail;
        _items = items;
    }

    public static InventoryAudit Of(InventorySystem inv)
    {
        var mail = new Dictionary<MailId, int>();
        var items = new Dictionary<ItemDefId, int>();
        foreach (var container in inv.Containers)
        {
            foreach (var entry in container.Entries)
                Add(entry.Stack, mail, items, +1);
        }

        return new InventoryAudit(mail, items);
    }

    public InventoryAudit Plus(Stack stack)
    {
        var mail = CopyMail();
        var items = CopyItems();
        Add(stack, mail, items, +1);
        return new InventoryAudit(mail, items);
    }

    public InventoryAudit Minus(Stack stack)
    {
        var mail = CopyMail();
        var items = CopyItems();
        Add(stack, mail, items, -1);
        return new InventoryAudit(mail, items);
    }

    internal InventoryAudit After(GridContainer container, Change change)
    {
        switch (change)
        {
            case Remove r:
                if (!container.TryGetEntry(r.Id, out var removed))
                    throw new InvalidOperationException("Plan removed an unknown entry.");
                return Minus(removed.Stack);
            case Upsert u:
                var next = this;
                if (container.TryGetEntry(u.Entry.Id, out var old))
                    next = next.Minus(old.Stack);
                return next.Plus(u.Entry.Stack);
            case Reset reset:
                var acc = this;
                foreach (var entry in container.Entries)
                    acc = acc.Minus(entry.Stack);
                foreach (var entry in reset.Entries)
                    acc = acc.Plus(entry.Stack);
                return acc;
            default:
                throw new NotSupportedException(change.GetType().Name);
        }
    }

    public bool Equals(InventoryAudit? other)
    {
        if (other is null) return false;
        if (_mail.Count != other._mail.Count || _items.Count != other._items.Count)
            return false;
        foreach (var pair in _mail)
        {
            if (!other._mail.TryGetValue(pair.Key, out var n) || n != pair.Value)
                return false;
        }

        foreach (var pair in _items)
        {
            if (!other._items.TryGetValue(pair.Key, out var n) || n != pair.Value)
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as InventoryAudit);

    public override int GetHashCode()
    {
        int hash = _mail.Count * 397 ^ _items.Count;
        foreach (var pair in _mail)
            hash ^= pair.Key.GetHashCode() ^ pair.Value;
        foreach (var pair in _items)
            hash ^= pair.Key.GetHashCode() ^ pair.Value;
        return hash;
    }

    public override string ToString()
    {
        int mail = 0;
        foreach (var n in _mail.Values) mail += n;
        int items = 0;
        foreach (var n in _items.Values) items += n;
        return $"mail={mail} items={items}";
    }

    private Dictionary<MailId, int> CopyMail() => new(_mail);

    private Dictionary<ItemDefId, int> CopyItems() => new(_items);

    private static void Add(
        Stack stack,
        Dictionary<MailId, int> mail,
        Dictionary<ItemDefId, int> items,
        int sign)
    {
        if (stack is MailStack letters)
        {
            for (int i = 0; i < letters.Ids.Count; i++)
            {
                var id = letters.Ids[i];
                mail.TryGetValue(id, out var n);
                n += sign;
                if (n == 0) mail.Remove(id);
                else mail[id] = n;
            }

            return;
        }

        if (stack is ItemStack item)
        {
            items.TryGetValue(item.Item, out var n);
            n += sign * item.Count;
            if (n == 0) items.Remove(item.Item);
            else items[item.Item] = n;
        }
    }
}
