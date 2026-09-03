using System;
using System.IO;

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
    }

    private static string RequireFile(string contentRoot, string relative)
    {
        string path = Path.Combine(contentRoot, relative);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing content file: {relative}");
        return path;
    }
}
