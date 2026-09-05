using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Content;

public sealed class ContentIdMap
{
    private readonly Dictionary<string, ItemDefId> _items;
    private readonly Dictionary<string, MailKindId> _mail;

    private ContentIdMap(
        Dictionary<string, ItemDefId> items,
        Dictionary<string, MailKindId> mail)
    {
        _items = items;
        _mail = mail;
        Items = items;
    }

    public IReadOnlyDictionary<string, ItemDefId> Items { get; }

    public static ContentIdMap Build(ContentBundle bundle)
    {
        if (bundle is null) throw new ArgumentNullException(nameof(bundle));

        var names = new string[bundle.Items.Length];
        for (int i = 0; i < bundle.Items.Length; i++)
            names[i] = bundle.Items[i].Id;
        Array.Sort(names, StringComparer.Ordinal);

        var items = new Dictionary<string, ItemDefId>(names.Length, StringComparer.Ordinal);
        for (int i = 0; i < names.Length; i++)
        {
            int ordinal = i + 1;
            if (ordinal > ushort.MaxValue)
                throw new InvalidOperationException("Item ordinal exceeded ItemDefId range.");
            items[names[i]] = new ItemDefId((ushort)ordinal);
        }

        var mail = new Dictionary<string, MailKindId>(bundle.Kinds.Length, StringComparer.Ordinal);
        for (int i = 0; i < bundle.Kinds.Length; i++)
        {
            string id = bundle.Kinds[i].Id;
            if (TryPinMail(id, out var kind))
                mail[id] = kind;
        }

        return new ContentIdMap(items, mail);
    }

    public bool TryItem(string contentId, out ItemDefId id)
    {
        if (contentId is null)
        {
            id = default;
            return false;
        }

        return _items.TryGetValue(contentId, out id);
    }

    public bool TryMail(string contentId, out MailKindId id)
    {
        if (contentId is null)
        {
            id = default;
            return false;
        }

        return _mail.TryGetValue(contentId, out id);
    }

    private static bool TryPinMail(string contentId, out MailKindId id)
    {
        switch (contentId)
        {
            case "letter":
                id = MailKinds.Letter;
                return true;
            case "postcard":
                id = MailKinds.Postcard;
                return true;
            case "small":
                id = MailKinds.SmallPackage;
                return true;
            case "medium":
                id = MailKinds.MediumPackage;
                return true;
            case "large":
                id = MailKinds.LargePackage;
                return true;
            case "cargo":
                id = MailKinds.Cargo;
                return true;
            default:
                id = default;
                return false;
        }
    }
}
