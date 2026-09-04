using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Content;

public sealed class ItemToolDef
{
    public ItemToolDef(string[] harvests, double yieldMultiplier, double swingTime)
    {
        Harvests = harvests;
        YieldMultiplier = yieldMultiplier;
        SwingTime = swingTime;
    }

    public string[] Harvests { get; }

    public double YieldMultiplier { get; }

    public double SwingTime { get; }
}

public sealed class ItemWeaponDef
{
    public ItemWeaponDef(
        double damage,
        double rate,
        double range,
        double arcDeg,
        string? ammoItem,
        int? shotsPerAmmo,
        double? aoeRadius,
        double? bonusVsTank)
    {
        Damage = damage;
        Rate = rate;
        Range = range;
        ArcDeg = arcDeg;
        AmmoItem = ammoItem;
        ShotsPerAmmo = shotsPerAmmo;
        AoeRadius = aoeRadius;
        BonusVsTank = bonusVsTank;
    }

    public double Damage { get; }

    public double Rate { get; }

    public double Range { get; }

    public double ArcDeg { get; }

    public string? AmmoItem { get; }

    public int? ShotsPerAmmo { get; }

    public double? AoeRadius { get; }

    public double? BonusVsTank { get; }
}

public sealed class ItemConsumableDef
{
    public ItemConsumableDef(double healInstant, double healOverTime, double duration)
    {
        HealInstant = healInstant;
        HealOverTime = healOverTime;
        Duration = duration;
    }

    public double HealInstant { get; }

    public double HealOverTime { get; }

    public double Duration { get; }
}

public sealed class ItemDef
{
    public ItemDef(
        string id,
        string name,
        StackCategory category,
        Footprint grid,
        int maxStack,
        WeightClass weight,
        int sellPrice,
        int? buyPrice,
        ItemToolDef? tool,
        ItemWeaponDef? weapon,
        ItemConsumableDef? consumable,
        string[] tags)
    {
        Id = id;
        Name = name;
        Category = category;
        Grid = grid;
        MaxStack = maxStack;
        Weight = weight;
        SellPrice = sellPrice;
        BuyPrice = buyPrice;
        Tool = tool;
        Weapon = weapon;
        Consumable = consumable;
        Tags = tags;
    }

    public string Id { get; }

    public string Name { get; }

    public StackCategory Category { get; }

    public Footprint Grid { get; }

    public int MaxStack { get; }

    public WeightClass Weight { get; }

    public int SellPrice { get; }

    public int? BuyPrice { get; }

    public ItemToolDef? Tool { get; }

    public ItemWeaponDef? Weapon { get; }

    public ItemConsumableDef? Consumable { get; }

    public string[] Tags { get; }
}

public static class ItemCatalog
{
    public const string RelativeDir = "items";

    public static ItemDef[] LoadDir(string dir)
    {
        ContentIds.RequireDirectory(dir);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new List<ItemDef>();
        foreach (string path in ContentIds.EnumerateJsonFiles(dir, skipFileName: "containers.json"))
            defs.AddRange(Parse(ContentIds.ReadFile(path), path, seen));

        if (defs.Count == 0)
            throw new InvalidOperationException($"{dir}: expected at least one item def.");
        return defs.ToArray();
    }

    public static ItemDef[] Parse(string json, string source)
        => Parse(json, source, new HashSet<string>(StringComparer.Ordinal));

    private static ItemDef[] Parse(string json, string source, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativeDir;
        var docs = ContentIds.ReadDocuments(json, source);
        var defs = new ItemDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<ItemDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static ItemDef Read(ItemDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        int? buyPrice = null;
        if (doc.BuyPrice is int listed)
            buyPrice = ContentIds.RequirePrice(listed, source, id, "buyPrice");

        return new ItemDef(
            id,
            ContentIds.RequireName(doc.Name, source, id),
            ContentIds.ParseItemCategory(doc.Category, source, id),
            ContentIds.RequireGrid(doc.Grid, source, id, "grid"),
            ContentIds.RequireMaxStack(doc.MaxStack, source, id),
            ContentIds.ParseWeight(doc.WeightClass, source, id),
            ContentIds.RequirePrice(doc.SellPrice, source, id, "sellPrice"),
            buyPrice,
            doc.Tool is null ? null : ReadTool(doc.Tool, source, id),
            doc.Weapon is null ? null : ReadWeapon(doc.Weapon, source, id),
            doc.Consumable is null ? null : ReadConsumable(doc.Consumable, source, id),
            ContentIds.ReadTags(doc.Tags, source, id));
    }

    private static ItemToolDef ReadTool(ToolDocument doc, string source, string id)
    {
        return new ItemToolDef(
            ContentIds.ReadIdList(doc.Harvests, source, id, "tool.harvests", required: true),
            ContentIds.RequireFiniteNonNegative(doc.YieldMultiplier, source, id, "tool.yieldMultiplier"),
            ContentIds.RequireFiniteNonNegative(doc.SwingTime, source, id, "tool.swingTime"));
    }

    private static ItemWeaponDef ReadWeapon(WeaponDocument doc, string source, string id)
    {
        string? ammoItem = null;
        int? shotsPerAmmo = null;
        double? aoeRadius = null;
        if (doc.Ranged is { } ranged)
        {
            ammoItem = ContentIds.OptionalContentId(ranged.AmmoItem, source);
            if (ranged.ShotsPerAmmo is int shots)
            {
                if (shots < 1)
                    throw new InvalidOperationException($"{source}: '{id}' weapon.ranged.shotsPerAmmo must be >= 1.");
                shotsPerAmmo = shots;
            }

            if (ranged.AoeRadius is double radius)
                aoeRadius = ContentIds.RequireFiniteNonNegative(radius, source, id, "weapon.ranged.aoeRadius");
        }

        double? bonusVsTank = null;
        if (doc.BonusVs?.Tank is double tank)
            bonusVsTank = ContentIds.RequireFiniteNonNegative(tank, source, id, "weapon.bonusVs.tank");

        return new ItemWeaponDef(
            ContentIds.RequireFiniteNonNegative(doc.Damage, source, id, "weapon.damage"),
            ContentIds.RequireFiniteNonNegative(doc.Rate, source, id, "weapon.rate"),
            ContentIds.RequireFiniteNonNegative(doc.Range, source, id, "weapon.range"),
            ContentIds.RequireFiniteNonNegative(doc.ArcDeg, source, id, "weapon.arcDeg"),
            ammoItem,
            shotsPerAmmo,
            aoeRadius,
            bonusVsTank);
    }

    private static ItemConsumableDef ReadConsumable(ConsumableDocument doc, string source, string id)
    {
        return new ItemConsumableDef(
            ContentIds.RequireFiniteNonNegative(doc.HealInstant, source, id, "consumable.healInstant"),
            ContentIds.RequireFiniteNonNegative(doc.HealOverTime, source, id, "consumable.healOverTime"),
            ContentIds.RequireFiniteNonNegative(doc.Duration, source, id, "consumable.duration"));
    }

    private sealed class ItemDocument
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public int[]? Grid { get; set; }
        public int MaxStack { get; set; }
        public string? WeightClass { get; set; }
        public int SellPrice { get; set; }
        public int? BuyPrice { get; set; }
        public ToolDocument? Tool { get; set; }
        public WeaponDocument? Weapon { get; set; }
        public ConsumableDocument? Consumable { get; set; }
        public string[]? Tags { get; set; }
    }

    private sealed class ToolDocument
    {
        public string[]? Harvests { get; set; }
        public double YieldMultiplier { get; set; }
        public double SwingTime { get; set; }
    }

    private sealed class WeaponDocument
    {
        public double Damage { get; set; }
        public double Rate { get; set; }
        public double Range { get; set; }
        public double ArcDeg { get; set; }
        public RangedDocument? Ranged { get; set; }
        public BonusVsDocument? BonusVs { get; set; }
    }

    private sealed class RangedDocument
    {
        public string? AmmoItem { get; set; }
        public int? ShotsPerAmmo { get; set; }
        public double? AoeRadius { get; set; }
    }

    private sealed class BonusVsDocument
    {
        public double? Tank { get; set; }
    }

    private sealed class ConsumableDocument
    {
        public double HealInstant { get; set; }
        public double HealOverTime { get; set; }
        public double Duration { get; set; }
    }
}
