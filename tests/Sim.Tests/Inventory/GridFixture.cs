using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Tests.Inventory;

internal sealed class GridFixture
{
    private uint _nextEntry = 1;
    private uint _nextMail = 1;

    public GridFixture(ContainerSpec? spec = null)
    {
        Catalog = TestStackCatalog.Default;
        Container = new GridContainer(new ContainerId(1), spec ?? ContainerSpec.Chest, Catalog);
    }

    public IStackCatalog Catalog { get; }

    public GridContainer Container { get; }

    public EntryId NextEntry() => new(_nextEntry++);

    public MailId NextMail() => new(_nextMail++);

    public MailStack LetterStack(AddressId address, int count)
    {
        var ids = new MailId[count];
        for (int i = 0; i < count; i++) ids[i] = NextMail();
        return new MailStack(TestStackCatalog.Letter, address, ids);
    }

    public MailStack LetterStack(AddressId address, Amount amount)
        => LetterStack(address, amount.Resolve(TestStackCatalog.Default.MaxStackOf(
            StackKey.Mail(TestStackCatalog.Letter, address))));

    public MailStack SmallPackage(AddressId address)
        => MailStack.Single(TestStackCatalog.SmallPackage, address, NextMail());

    public bool TryCommit(Change change)
    {
        if (!Container.Apply(change)) return false;
        Container.Bump();
        return true;
    }

    public bool TryPlace(Stack stack, byte x, byte y, out EntryId id)
        => TryPlace(stack, x, y, rotated: false, out id);

    public bool TryPlace(Stack stack, byte x, byte y, bool rotated, out EntryId id)
    {
        id = NextEntry();
        var fp = Catalog.FootprintOf(stack.Key);
        var at = Placement.For(fp, x, y, rotated);
        return TryCommit(new Upsert(new Entry(id, stack, at)));
    }

    public bool TryRotate(EntryId id, bool rotated)
    {
        if (!Container.TryGetEntry(id, out var entry)) return false;
        var fp = Catalog.FootprintOf(entry.Stack.Key);
        var at = Placement.For(fp, entry.At.X, entry.At.Y, rotated);
        return TryCommit(new Upsert(new Entry(entry.Id, entry.Stack, at)));
    }

    public bool TryMerge(EntryId target, Stack incoming, out Stack? leftover)
    {
        if (!Container.TryGetEntry(target, out var entry) || !entry.Stack.Key.Equals(incoming.Key))
        {
            leftover = incoming;
            return false;
        }

        int room = Catalog.MaxStackOf(entry.Stack.Key) - entry.Stack.Count;
        if (room <= 0)
        {
            leftover = incoming;
            return false;
        }

        int k = Math.Min(room, incoming.Count);
        var taken = incoming.Take(k, out leftover);
        return TryCommit(new Upsert(new Entry(entry.Id, entry.Stack.Merge(taken), entry.At)));
    }

    public bool TryApplyFit(Stack incoming, bool allowPartial, out Stack? leftover)
    {
        var changes = Container.PlanFit(incoming, allowPartial, NextEntry, out leftover);
        if (changes.Count == 0) return false;
        foreach (var change in changes)
        {
            if (Container.Apply(change)) continue;
            throw new InvalidOperationException("PlanFit produced a change that Apply rejected.");
        }

        Container.Bump();
        return true;
    }
}
