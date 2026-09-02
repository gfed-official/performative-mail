using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Sim.Tests.Inventory;

public sealed class TestStackCatalog : IStackCatalog
{
    public static readonly TestStackCatalog Default = new();

    public static readonly MailKindId Letter = MailKinds.Letter;
    public static readonly MailKindId Postcard = MailKinds.Postcard;
    public static readonly MailKindId SmallPackage = MailKinds.SmallPackage;
    public static readonly ItemDefId Log = new(1);

    public Footprint FootprintOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (key.Def == Letter.Value || key.Def == Postcard.Value) return new Footprint(1, 1);
            if (key.Def == SmallPackage.Value) return new Footprint(1, 2);
        }
        else if (key.Def == Log.Value)
        {
            return new Footprint(1, 1);
        }

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public int MaxStackOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (key.Def == Letter.Value) return 20;
            if (key.Def == Postcard.Value) return 40;
            if (key.Def == SmallPackage.Value) return 1;
        }
        else if (key.Def == Log.Value)
        {
            return 50;
        }

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public WeightClass WeightOf(StackKey key)
    {
        if (key.IsMail || key.Def == Log.Value) return WeightClass.Light;
        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public StackCategory CategoryOf(StackKey key)
        => key.IsMail ? StackCategory.Mail : StackCategory.Material;
}
