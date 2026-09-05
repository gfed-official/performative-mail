using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Content;

public sealed class ContentStackCatalogTests
{
    [Fact]
    public void Build_AssignsItemOrdinalsBySortedId()
    {
        var (bundle, ids, _) = Load();
        var sorted = bundle.Items.Select(i => i.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(sorted);
        Assert.Equal(sorted.Length, ids.Items.Count);
        for (int i = 0; i < sorted.Length; i++)
        {
            Assert.True(ids.TryItem(sorted[i], out var id));
            Assert.Equal((ushort)(i + 1), id.Value);
            Assert.Equal(id, ids.Items[sorted[i]]);
        }
    }

    [Fact]
    public void TryMail_PinsKnownKindsToMailKinds()
    {
        var (_, ids, _) = Load();

        Assert.True(ids.TryMail("letter", out var letter));
        Assert.Equal(MailKinds.Letter, letter);
        Assert.True(ids.TryMail("postcard", out var postcard));
        Assert.Equal(MailKinds.Postcard, postcard);
        Assert.True(ids.TryMail("small", out var small));
        Assert.Equal(MailKinds.SmallPackage, small);
        Assert.True(ids.TryMail("medium", out var medium));
        Assert.Equal(MailKinds.MediumPackage, medium);
        Assert.True(ids.TryMail("large", out var large));
        Assert.Equal(MailKinds.LargePackage, large);
        Assert.True(ids.TryMail("cargo", out var cargo));
        Assert.Equal(MailKinds.Cargo, cargo);
        Assert.False(ids.TryMail("no_such_kind", out _));
    }

    [Theory]
    [InlineData("letter")]
    [InlineData("postcard")]
    [InlineData("small")]
    [InlineData("medium")]
    public void Catalog_MailParity_MatchesMailStackCatalog(string contentId)
    {
        var (_, ids, catalog) = Load();
        Assert.True(ids.TryMail(contentId, out var kind));
        var key = StackKey.Mail(kind, default);

        Assert.Equal(MailStackCatalog.Default.FootprintOf(key), catalog.FootprintOf(key));
        Assert.Equal(MailStackCatalog.Default.MaxStackOf(key), catalog.MaxStackOf(key));
        Assert.Equal(MailStackCatalog.Default.WeightOf(key), catalog.WeightOf(key));
        Assert.Equal(MailStackCatalog.Default.CategoryOf(key), catalog.CategoryOf(key));
    }

    [Theory]
    [InlineData("axe")]
    [InlineData("log")]
    [InlineData("bandage")]
    [InlineData("stone")]
    public void Catalog_Items_ResolveFromBundle(string contentId)
    {
        var (bundle, ids, catalog) = Load();
        var def = Assert.Single(bundle.Items, i => i.Id == contentId);
        Assert.True(ids.TryItem(contentId, out var id));
        var key = StackKey.Item(id);

        Assert.Equal(def.Grid, catalog.FootprintOf(key));
        Assert.Equal(def.MaxStack, catalog.MaxStackOf(key));
        Assert.Equal(def.Weight, catalog.WeightOf(key));
        Assert.Equal(def.Category, catalog.CategoryOf(key));
    }

    [Fact]
    public void Catalog_UnknownKey_Throws()
    {
        var (_, _, catalog) = Load();
        var key = StackKey.Item(new ItemDefId(ushort.MaxValue));
        Assert.Throws<ArgumentException>(() => catalog.FootprintOf(key));
        Assert.Throws<ArgumentException>(() => catalog.MaxStackOf(key));
        Assert.Throws<ArgumentException>(() => catalog.WeightOf(key));
        Assert.Throws<ArgumentException>(() => catalog.CategoryOf(key));
    }

    private static (ContentBundle Bundle, ContentIdMap Ids, ContentStackCatalog Catalog) Load()
    {
        var bundle = ContentFiles.Load(FindContentRoot());
        var ids = ContentIdMap.Build(bundle);
        return (bundle, ids, ContentStackCatalog.From(bundle, ids));
    }

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, ArchetypeCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/archetypes.json");
    }
}
