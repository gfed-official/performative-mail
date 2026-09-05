using PerformativeMail.App;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Net.Tests.App;

public sealed class ContentBootTests
{
    [Fact]
    public void Load_BuildsContentStackCatalogFromRepoContent()
    {
        var bundle = ContentBoot.Load(out var ids, out var catalog);
        Assert.True(ids.TryItem("axe", out _));
        Assert.True(ids.TryMail("letter", out _));
        Assert.NotEmpty(bundle.Items);
        Assert.IsType<ContentStackCatalog>(catalog);
        Assert.Equal(bundle.Items[0].Grid, catalog.FootprintOf(StackKey.Item(ids.Items[bundle.Items[0].Id])));
    }
}
