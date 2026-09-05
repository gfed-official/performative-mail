using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Inventory;

public sealed class ContentStackCatalog : IStackCatalog
{
    private readonly Dictionary<ushort, ItemDef> _items;
    private readonly Dictionary<ushort, MailKindDef> _mail;

    private ContentStackCatalog(
        Dictionary<ushort, ItemDef> items,
        Dictionary<ushort, MailKindDef> mail)
    {
        _items = items;
        _mail = mail;
    }

    public static ContentStackCatalog From(ContentBundle bundle, ContentIdMap ids)
    {
        if (bundle is null) throw new ArgumentNullException(nameof(bundle));
        if (ids is null) throw new ArgumentNullException(nameof(ids));

        var items = new Dictionary<ushort, ItemDef>(bundle.Items.Length);
        for (int i = 0; i < bundle.Items.Length; i++)
        {
            var def = bundle.Items[i];
            if (!ids.TryItem(def.Id, out var id))
                throw new InvalidOperationException($"Unmapped item '{def.Id}'.");
            items[id.Value] = def;
        }

        var mail = new Dictionary<ushort, MailKindDef>(bundle.Kinds.Length);
        for (int i = 0; i < bundle.Kinds.Length; i++)
        {
            var def = bundle.Kinds[i];
            if (!ids.TryMail(def.Id, out var id))
                throw new InvalidOperationException($"Unmapped mail kind '{def.Id}'.");
            mail[id.Value] = def;
        }

        return new ContentStackCatalog(items, mail);
    }

    public Footprint FootprintOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (_mail.TryGetValue(key.Def, out var mail))
                return mail.Grid;
        }
        else if (_items.TryGetValue(key.Def, out var item))
            return item.Grid;

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public int MaxStackOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (_mail.TryGetValue(key.Def, out var mail))
                return mail.MaxStack;
        }
        else if (_items.TryGetValue(key.Def, out var item))
            return item.MaxStack;

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public WeightClass WeightOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (_mail.TryGetValue(key.Def, out var mail))
                return mail.Weight;
        }
        else if (_items.TryGetValue(key.Def, out var item))
            return item.Weight;

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public StackCategory CategoryOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (_mail.ContainsKey(key.Def))
                return StackCategory.Mail;
        }
        else if (_items.TryGetValue(key.Def, out var item))
            return item.Category;

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }
}
