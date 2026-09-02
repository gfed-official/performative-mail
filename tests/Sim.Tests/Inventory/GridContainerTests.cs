using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using Xunit;

namespace PerformativeMail.Sim.Tests.Inventory;

public sealed class GridContainerTests
{
    private static readonly AddressId Oak = new(1, 4, 13, 0);
    private static readonly AddressId Elm = new(1, 5, 2, 0);

    [Fact]
    public void Place_Letter1x1_InBounds()
    {
        var fx = new GridFixture();
        Assert.True(fx.TryPlace(fx.LetterStack(Oak, 1), 0, 0, out var id));
        Assert.Equal(id, fx.Container.EntryAt(new Cell(0, 0)));
        Assert.True(fx.Container.TryGetEntry(id, out var entry));
        Assert.Equal(1, entry.Stack.Count);
        Assert.Null(fx.Container.CheckInvariants());
    }

    [Fact]
    public void Place_RejectsOutOfBoundsAndOverlap()
    {
        var fx = new GridFixture();
        Assert.True(fx.TryPlace(fx.LetterStack(Oak, 1), 0, 0, out var first));
        var version = fx.Container.Version;
        var hash = fx.Container.Hash;

        Assert.False(fx.TryPlace(fx.LetterStack(Oak, 1), 8, 0, out _));
        Assert.False(fx.TryPlace(fx.LetterStack(Elm, 1), 0, 0, out _));
        Assert.Equal(first, fx.Container.EntryAt(new Cell(0, 0)));
        Assert.Equal(version, fx.Container.Version);
        Assert.Equal(hash, fx.Container.Hash);
        Assert.Null(fx.Container.CheckInvariants());
    }

    [Fact]
    public void Rotate_1x2To2x1_WhenSpaceAllows_AndRejectsWhenBlocked()
    {
        var fx = new GridFixture();
        Assert.True(fx.TryPlace(fx.SmallPackage(Oak), 0, 0, out var id));
        Assert.Equal(id, fx.Container.EntryAt(new Cell(0, 1)));
        Assert.Equal(EntryId.None, fx.Container.EntryAt(new Cell(1, 0)));

        Assert.True(fx.TryRotate(id, rotated: true));
        Assert.Equal(id, fx.Container.EntryAt(new Cell(1, 0)));
        Assert.Equal(EntryId.None, fx.Container.EntryAt(new Cell(0, 1)));
        Assert.True(fx.Container.TryGetEntry(id, out var rotated));
        Assert.True(rotated.At.Rotated);
        Assert.Null(fx.Container.CheckInvariants());

        var blocked = new GridFixture();
        Assert.True(blocked.TryPlace(blocked.SmallPackage(Oak), 0, 0, out var package));
        Assert.True(blocked.TryPlace(blocked.LetterStack(Elm, 1), 1, 0, out _));
        var version = blocked.Container.Version;
        Assert.False(blocked.TryRotate(package, rotated: true));
        Assert.Equal(package, blocked.Container.EntryAt(new Cell(0, 1)));
        Assert.Equal(version, blocked.Container.Version);
        Assert.Null(blocked.Container.CheckInvariants());
    }

    [Fact]
    public void Stack_SameAddress_FillsUpToMax_LeftoverRemains()
    {
        var fx = new GridFixture();
        Assert.True(fx.TryPlace(fx.LetterStack(Oak, 18), 0, 0, out var id));
        Assert.True(fx.TryMerge(id, fx.LetterStack(Oak, Amount.Of(5)), out var leftover));
        Assert.True(fx.Container.TryGetEntry(id, out var merged));
        Assert.Equal(20, merged.Stack.Count);
        Assert.NotNull(leftover);
        Assert.Equal(3, leftover!.Count);
        Assert.Equal(EntryId.None, fx.Container.EntryAt(new Cell(1, 0)));
        Assert.Null(fx.Container.CheckInvariants());
    }

    [Fact]
    public void Stack_DifferentAddresses_DoesNotMerge()
    {
        var fx = new GridFixture();
        Assert.True(fx.TryPlace(fx.LetterStack(Oak, 4), 0, 0, out var oakId));
        var version = fx.Container.Version;
        var hash = fx.Container.Hash;
        var elm = fx.LetterStack(Elm, 2);
        var fp = fx.Catalog.FootprintOf(elm.Key);
        Assert.False(fx.TryCommit(new Upsert(new Entry(fx.NextEntry(), elm, Placement.For(fp, 0, 0, false)))));
        Assert.Equal(version, fx.Container.Version);
        Assert.Equal(hash, fx.Container.Hash);
        Assert.True(fx.Container.TryGetEntry(oakId, out var oak));
        Assert.Equal(4, oak.Stack.Count);

        Assert.True(fx.TryApplyFit(fx.LetterStack(Elm, 2), allowPartial: true, out var leftover));
        Assert.Null(leftover);
        Assert.Equal(oakId, fx.Container.EntryAt(new Cell(0, 0)));
        Assert.NotEqual(EntryId.None, fx.Container.EntryAt(new Cell(1, 0)));
        Assert.Null(fx.Container.CheckInvariants());
    }

    [Fact]
    public void QuickMove_MergesFirst_ThenUnrotated_ThenRotated_RowMajor()
    {
        var merge = new GridFixture();
        Assert.True(merge.TryPlace(merge.LetterStack(Oak, 19), 3, 0, out var existing));
        Assert.True(merge.TryApplyFit(merge.LetterStack(Oak, 2), allowPartial: true, out var leftover));
        Assert.True(merge.Container.TryGetEntry(existing, out var stacked));
        Assert.Equal(20, stacked.Stack.Count);
        Assert.Null(leftover);
        Assert.Equal(existing, merge.Container.EntryAt(new Cell(3, 0)));
        var spilled = merge.Container.EntryAt(new Cell(0, 0));
        Assert.NotEqual(EntryId.None, spilled);
        Assert.NotEqual(existing, spilled);
        Assert.True(merge.Container.TryGetEntry(spilled, out var overflow));
        Assert.Equal(1, overflow.Stack.Count);
        Assert.Null(merge.Container.CheckInvariants());

        var unrotated = new GridFixture();
        Assert.True(unrotated.TryApplyFit(unrotated.SmallPackage(Oak), allowPartial: false, out var packageLeft));
        Assert.Null(packageLeft);
        var packageId = unrotated.Container.EntryAt(new Cell(0, 0));
        Assert.Equal(packageId, unrotated.Container.EntryAt(new Cell(0, 1)));
        Assert.Equal(EntryId.None, unrotated.Container.EntryAt(new Cell(1, 0)));
        Assert.True(unrotated.Container.TryGetEntry(packageId, out var placed));
        Assert.False(placed.At.Rotated);
        Assert.Null(unrotated.Container.CheckInvariants());

        var rotated = new GridFixture(ContainerSpec.BaseInventory);
        for (byte x = 0; x < 8; x++)
            Assert.True(rotated.TryPlace(rotated.LetterStack(Elm, 1), x, 1, out _));
        Assert.True(rotated.TryApplyFit(rotated.SmallPackage(Oak), allowPartial: false, out var rotatedLeft));
        Assert.Null(rotatedLeft);
        var rotatedId = rotated.Container.EntryAt(new Cell(0, 0));
        Assert.NotEqual(EntryId.None, rotatedId);
        Assert.Equal(rotatedId, rotated.Container.EntryAt(new Cell(1, 0)));
        Assert.NotEqual(rotatedId, rotated.Container.EntryAt(new Cell(0, 1)));
        Assert.True(rotated.Container.TryGetEntry(rotatedId, out var rotatedEntry));
        Assert.True(rotatedEntry.At.Rotated);
        Assert.Null(rotated.Container.CheckInvariants());
    }

    [Fact]
    public void Apply_BumpsVersionAndHash_OnSuccessfulChange()
    {
        var fx = new GridFixture();
        Assert.Equal(0u, fx.Container.Version.Value);
        Assert.Equal(0ul, fx.Container.Hash);

        Assert.True(fx.TryPlace(fx.LetterStack(Oak, 1), 0, 0, out _));
        Assert.Equal(1u, fx.Container.Version.Value);
        Assert.NotEqual(0ul, fx.Container.Hash);
        var hash = fx.Container.Hash;

        Assert.True(fx.TryPlace(fx.LetterStack(Elm, 1), 1, 0, out _));
        Assert.Equal(2u, fx.Container.Version.Value);
        Assert.NotEqual(hash, fx.Container.Hash);
        Assert.Null(fx.Container.CheckInvariants());
    }

    [Fact]
    public void Hotbar_HandsCellIsBlocked()
    {
        var fx = new GridFixture(ContainerSpec.Hotbar);
        Assert.True(fx.Container.Spec.Shape.IsBlocked(new Cell(0, 0)));
        Assert.False(fx.Container.Spec.Shape.IsBlocked(new Cell(1, 0)));
        Assert.False(fx.TryPlace(fx.LetterStack(Oak, 1), 0, 0, out _));
        Assert.True(fx.TryPlace(fx.LetterStack(Oak, 1), 1, 0, out var id));
        Assert.Equal(id, fx.Container.EntryAt(new Cell(1, 0)));
        Assert.Equal(EntryId.None, fx.Container.EntryAt(new Cell(0, 0)));
        Assert.Null(fx.Container.CheckInvariants());
    }
}

public sealed class AmountAndIdTests
{
    [Fact]
    public void Amount_AllAndOf()
    {
        Assert.True(Amount.All.IsAll);
        Assert.Equal(20, Amount.All.Resolve(20));
        Assert.Equal(5, Amount.Of(5).Resolve(20));
        Assert.Throws<ArgumentOutOfRangeException>(() => Amount.Of(0));
    }

    [Fact]
    public void AddressId_PacksAndUnpacks()
    {
        var address = new AddressId(1, 4, 13, 2);
        Assert.Equal(address, AddressId.Unpack(address.Packed));
        Assert.Equal(new AddressId(2, 0, 1, 0), AddressId.Unpack(new AddressId(2, 0, 1, 0).Packed));
    }

    [Fact]
    public void MailStack_RejectsEmpty_AndTakeLeavesBase()
    {
        var address = new AddressId(1, 1, 1, 0);
        Assert.Throws<ArgumentException>(() => new MailStack(TestStackCatalog.Letter, address, Array.Empty<MailId>()));
        var stack = new MailStack(
            TestStackCatalog.Letter,
            address,
            new[] { new MailId(1), new MailId(2), new MailId(3) });
        var taken = (MailStack)stack.Take(1, out var rest);
        Assert.Equal(new MailId(3), taken.Ids[0]);
        Assert.NotNull(rest);
        Assert.Equal(2, rest!.Count);
        Assert.Equal(new MailId(1), ((MailStack)rest).Ids[0]);
    }
}
