using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class ContentBoot
{
    public static ContentBundle Load(out ContentIdMap ids, out ContentStackCatalog catalog)
    {
        var bundle = ContentFiles.Load(ContentRoot.Find());
        ids = ContentIdMap.Build(bundle);
        catalog = ContentStackCatalog.From(bundle, ids);
        return bundle;
    }
}
