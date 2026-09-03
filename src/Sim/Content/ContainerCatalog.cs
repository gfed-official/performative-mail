using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Content;

public enum ContainerView
{
    Grid,
    Manifest
}

public enum BeltAccess
{
    None,
    AnySide,
    LoadingFace,
    OutfeedOnly
}

public sealed class ContainerDef
{
    public ContainerDef(
        string id,
        Footprint grid,
        ContainerView view,
        BeltAccess beltAccess,
        StackCategory[]? allowedCategories)
    {
        Id = id;
        Grid = grid;
        View = view;
        BeltAccess = beltAccess;
        AllowedCategories = allowedCategories;
    }

    public string Id { get; }

    public Footprint Grid { get; }

    public ContainerView View { get; }

    public BeltAccess BeltAccess { get; }

    public StackCategory[]? AllowedCategories { get; }
}

public static class ContainerCatalog
{
    public const string RelativePath = "items/containers.json";

    public static ContainerDef[] LoadFile(string path)
        => Parse(ContentIds.ReadFile(path), path);

    public static ContainerDef[] Parse(string json, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativePath;
        var docs = ContentIds.ReadDocuments(json, source);
        if (docs.Length == 0)
            throw new InvalidOperationException($"{source}: expected a non-empty array of container defs.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new ContainerDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<ContainerDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static ContainerDef Read(ContainerDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        return new ContainerDef(
            id,
            ContentIds.RequireGrid(doc.Grid, source, id, "grid"),
            ParseView(doc.View, source, id),
            ParseBeltAccess(doc.BeltAccess, source, id),
            ReadAllowed(doc.AllowedCategories, source, id));
    }

    private static ContainerView ParseView(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(raw, source, $"'{id}' view", "grid", "manifest");
        return token == "grid" ? ContainerView.Grid : ContainerView.Manifest;
    }

    private static BeltAccess ParseBeltAccess(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(
            raw,
            source,
            $"'{id}' beltAccess",
            "none",
            "any_side",
            "loading_face",
            "outfeed_only");
        return token switch
        {
            "none" => BeltAccess.None,
            "any_side" => BeltAccess.AnySide,
            "loading_face" => BeltAccess.LoadingFace,
            "outfeed_only" => BeltAccess.OutfeedOnly,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown beltAccess '{raw}'.")
        };
    }

    private static StackCategory[]? ReadAllowed(string[]? raw, string source, string id)
    {
        if (raw is null) return null;
        var allowed = new StackCategory[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            allowed[i] = ContentIds.ParseStackCategory(raw[i], source, id, i);
        return allowed;
    }

    private sealed class ContainerDocument
    {
        public string? Id { get; set; }
        public int[]? Grid { get; set; }
        public string? View { get; set; }
        public string? BeltAccess { get; set; }
        public string[]? AllowedCategories { get; set; }
    }
}
