using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Mail;

public sealed class MailStackCatalog : IStackCatalog
{
    public static MailStackCatalog Default { get; } = new();

    public Footprint FootprintOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (key.Def == MailKinds.Letter.Value || key.Def == MailKinds.Postcard.Value)
                return new Footprint(1, 1);
            if (key.Def == MailKinds.SmallPackage.Value)
                return new Footprint(1, 2);
            if (key.Def == MailKinds.MediumPackage.Value)
                return new Footprint(2, 2);
            if (key.Def == MailKinds.LargePackage.Value)
                return new Footprint(2, 4);
            if (key.Def == MailKinds.Cargo.Value)
                return new Footprint(5, 8);
        }

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public int MaxStackOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (key.Def == MailKinds.Letter.Value) return 20;
            if (key.Def == MailKinds.Postcard.Value) return 40;
            if (key.Def == MailKinds.SmallPackage.Value) return 1;
            if (key.Def == MailKinds.MediumPackage.Value) return 1;
            if (key.Def == MailKinds.LargePackage.Value) return 1;
            if (key.Def == MailKinds.Cargo.Value) return 1;
        }

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public WeightClass WeightOf(StackKey key)
    {
        if (!key.IsMail)
            throw new ArgumentException("Unknown stack key.", nameof(key));
        if (key.Def == MailKinds.Cargo.Value) return WeightClass.Bulk;
        if (key.Def == MailKinds.LargePackage.Value) return WeightClass.Heavy;
        if (key.Def == MailKinds.MediumPackage.Value) return WeightClass.Medium;
        return WeightClass.Light;
    }

    public StackCategory CategoryOf(StackKey key)
        => key.IsMail ? StackCategory.Mail : StackCategory.Material;
}
