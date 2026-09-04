using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Content;

public enum ShopKind
{
    Blueprint,
    Item,
    Vehicle,
    Hire,
    Service
}

public enum ShopSlot
{
    Fixed,
    Rotating
}

public sealed class ShopItemDef
{
    public ShopItemDef(
        string id,
        string name,
        ShopKind kind,
        int price,
        string? grantItem,
        int? grantCount,
        string? grantBlueprint,
        string? grantVehicle,
        int fromShift,
        ShopSlot slot,
        bool oncePerRun,
        string[] tags)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Price = price;
        GrantItem = grantItem;
        GrantCount = grantCount;
        GrantBlueprint = grantBlueprint;
        GrantVehicle = grantVehicle;
        FromShift = fromShift;
        Slot = slot;
        OncePerRun = oncePerRun;
        Tags = tags;
    }

    public string Id { get; }

    public string Name { get; }

    public ShopKind Kind { get; }

    public int Price { get; }

    public string? GrantItem { get; }

    public int? GrantCount { get; }

    public string? GrantBlueprint { get; }

    public string? GrantVehicle { get; }

    public int FromShift { get; }

    public ShopSlot Slot { get; }

    public bool OncePerRun { get; }

    public string[] Tags { get; }
}

public static class ShopCatalog
{
    public const string RelativeDir = "shop";

    public static ShopItemDef[] LoadDir(string dir)
    {
        ContentIds.RequireDirectory(dir);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new List<ShopItemDef>();
        foreach (string path in ContentIds.EnumerateJsonFiles(dir))
            defs.AddRange(Parse(ContentIds.ReadFile(path), path, seen));

        if (defs.Count == 0)
            throw new InvalidOperationException($"{dir}: expected at least one shop def.");
        return defs.ToArray();
    }

    public static ShopItemDef[] Parse(string json, string source)
        => Parse(json, source, new HashSet<string>(StringComparer.Ordinal));

    private static ShopItemDef[] Parse(string json, string source, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativeDir;
        var docs = ContentIds.ReadDocuments(json, source);
        var defs = new ShopItemDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<ShopDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static ShopItemDef Read(ShopDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        ShopKind kind = ParseKind(doc.Kind, source, id);
        if (kind is ShopKind.Hire or ShopKind.Service)
        {
            throw new InvalidOperationException(
                $"{source}: '{id}' kind '{doc.Kind}' has no target def in this unit.");
        }
        if (doc.Availability is null)
            throw new InvalidOperationException($"{source}: '{id}' availability is required.");
        if (doc.Availability.FromShift < 1)
            throw new InvalidOperationException($"{source}: '{id}' availability.fromShift must be >= 1.");

        string? grantItem = ContentIds.OptionalContentId(doc.Grants?.Item, source);
        string? grantBlueprint = ContentIds.OptionalContentId(doc.Grants?.Blueprint, source);
        string? grantVehicle = ContentIds.OptionalContentId(doc.Grants?.Vehicle, source);
        int? grantCount = null;

        if (kind == ShopKind.Item)
        {
            if (grantItem is null)
                throw new InvalidOperationException($"{source}: '{id}' kind item requires grants.item.");
            int count = doc.Grants?.Count ?? 1;
            if (count < 1)
                throw new InvalidOperationException($"{source}: '{id}' grants.count must be >= 1.");
            grantCount = count;
        }
        else if (kind == ShopKind.Blueprint && grantBlueprint is null)
        {
            throw new InvalidOperationException($"{source}: '{id}' kind blueprint requires grants.blueprint.");
        }
        else if (kind == ShopKind.Vehicle)
        {
            if (grantVehicle is null)
                throw new InvalidOperationException($"{source}: '{id}' kind vehicle requires grants.vehicle.");
            if (!string.Equals(grantVehicle, "bike", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{source}: '{id}' grants.vehicle '{grantVehicle}' is not bike.");
            }
        }
        else if (doc.Grants?.Count is int listed)
        {
            if (listed < 1)
                throw new InvalidOperationException($"{source}: '{id}' grants.count must be >= 1.");
            grantCount = listed;
        }

        return new ShopItemDef(
            id,
            ContentIds.RequireName(doc.Name, source, id),
            kind,
            ContentIds.RequirePrice(doc.Price, source, id, "price"),
            grantItem,
            grantCount,
            grantBlueprint,
            grantVehicle,
            doc.Availability.FromShift,
            ParseSlot(doc.Availability.Slot, source, id),
            doc.OncePerRun,
            ContentIds.ReadTags(doc.Tags, source, id));
    }

    private static ShopKind ParseKind(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(
            raw,
            source,
            $"'{id}' kind",
            "blueprint",
            "item",
            "vehicle",
            "hire",
            "service");
        return token switch
        {
            "blueprint" => ShopKind.Blueprint,
            "item" => ShopKind.Item,
            "vehicle" => ShopKind.Vehicle,
            "hire" => ShopKind.Hire,
            "service" => ShopKind.Service,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown kind '{raw}'.")
        };
    }

    private static ShopSlot ParseSlot(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(raw, source, $"'{id}' availability.slot", "fixed", "rotating");
        return token == "fixed" ? ShopSlot.Fixed : ShopSlot.Rotating;
    }

    private sealed class ShopDocument
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public int Price { get; set; }
        public GrantsDocument? Grants { get; set; }
        public AvailabilityDocument? Availability { get; set; }
        public bool OncePerRun { get; set; }
        public string[]? Tags { get; set; }
    }

    private sealed class GrantsDocument
    {
        public string? Item { get; set; }
        public int? Count { get; set; }
        public string? Blueprint { get; set; }
        public string? Vehicle { get; set; }
    }

    private sealed class AvailabilityDocument
    {
        public int FromShift { get; set; }
        public string? Slot { get; set; }
    }
}
