using System;
using System.IO;
using PerformativeMail.Sim.Content;

namespace PerformativeMail.Sim.World;

public static class ContentFiles
{
    public static void Validate(string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(contentRoot))
            throw new ArgumentException("Content root is required.", nameof(contentRoot));
        if (!Directory.Exists(contentRoot))
            throw new InvalidOperationException($"Content root not found. Path was {contentRoot}");

        StreetCatalog.LoadFile(RequireFile(contentRoot, StreetCatalog.RelativePath));
        ArchetypeCatalog.LoadFile(RequireFile(contentRoot, ArchetypeCatalog.RelativePath));
        BalanceCatalog.LoadFile(RequireFile(contentRoot, BalanceCatalog.RelativePath));

        var items = ItemCatalog.LoadDir(RequireDir(contentRoot, ItemCatalog.RelativeDir));
        var containers = ContainerCatalog.LoadFile(RequireFile(contentRoot, ContainerCatalog.RelativePath));
        var kinds = MailKindCatalog.LoadFile(RequireFile(contentRoot, MailKindCatalog.RelativePath));
        var mix = MailMixCatalog.LoadFile(RequireFile(contentRoot, MailMixCatalog.RelativePath));
        var dests = DestinationTypeCatalog.LoadFile(RequireFile(contentRoot, DestinationTypeCatalog.RelativePath));
        var buildings = BuildingCatalog.LoadDir(RequireDir(contentRoot, BuildingCatalog.RelativeDir));
        var recipes = RecipeCatalog.LoadDir(RequireDir(contentRoot, RecipeCatalog.RelativeDir));
        var shop = ShopCatalog.LoadDir(RequireDir(contentRoot, ShopCatalog.RelativeDir));
        ContentRefs.Validate(items, containers, kinds, mix, dests, buildings, recipes, shop);
    }

    private static string RequireFile(string contentRoot, string relative)
    {
        string path = Path.Combine(contentRoot, relative);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing content file: {relative}");
        return path;
    }

    private static string RequireDir(string contentRoot, string relative)
    {
        string path = Path.Combine(contentRoot, relative);
        if (!Directory.Exists(path))
            throw new InvalidOperationException($"Missing content directory: {relative}");
        return path;
    }
}
