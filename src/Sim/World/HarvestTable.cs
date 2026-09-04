using System;

namespace PerformativeMail.Sim.World;

public enum HarvestTool : byte
{
    Hand = 1,
    Axe = 2,
    Pickaxe = 3,
    Shovel = 4
}

[Flags]
public enum HarvestTools : byte
{
    None = 0,
    Hand = 1,
    Axe = 2,
    Pickaxe = 4,
    Shovel = 8
}

public enum HarvestRemnant : byte
{
    Live = 0,
    Stump = 1,
    Gone = 2,
    RegrowNextShift = 3
}

public readonly record struct HarvestSpec
{
    public HarvestSpec(
        ResourceKind kind,
        string itemId,
        int yieldPerHit,
        int hits,
        HarvestTools tools,
        HarvestRemnant after)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Item id is required.", nameof(itemId));
        if (yieldPerHit < 1)
            throw new ArgumentOutOfRangeException(nameof(yieldPerHit), yieldPerHit, null);
        if (hits < 1)
            throw new ArgumentOutOfRangeException(nameof(hits), hits, null);
        if (tools == HarvestTools.None)
            throw new ArgumentOutOfRangeException(nameof(tools), tools, null);
        if (after == HarvestRemnant.Live)
            throw new ArgumentOutOfRangeException(nameof(after), after, null);

        Kind = kind;
        ItemId = itemId;
        YieldPerHit = yieldPerHit;
        Hits = hits;
        Tools = tools;
        After = after;
    }

    public ResourceKind Kind { get; }

    public string ItemId { get; }

    public int YieldPerHit { get; }

    public int Hits { get; }

    public HarvestTools Tools { get; }

    public HarvestRemnant After { get; }

    public bool Allows(HarvestTool tool) => tool switch
    {
        HarvestTool.Hand => (Tools & HarvestTools.Hand) != 0,
        HarvestTool.Axe => (Tools & HarvestTools.Axe) != 0,
        HarvestTool.Pickaxe => (Tools & HarvestTools.Pickaxe) != 0,
        HarvestTool.Shovel => (Tools & HarvestTools.Shovel) != 0,
        _ => false
    };
}

public static class HarvestTable
{
    public static HarvestSpec Of(ResourceKind kind) => kind switch
    {
        ResourceKind.Wood => new HarvestSpec(
            ResourceKind.Wood, "log", 2, 5, HarvestTools.Axe | HarvestTools.Hand, HarvestRemnant.Stump),
        ResourceKind.Fiber => new HarvestSpec(
            ResourceKind.Fiber, "fiber", 3, 3, HarvestTools.Hand, HarvestRemnant.RegrowNextShift),
        ResourceKind.Stone => new HarvestSpec(
            ResourceKind.Stone, "stone", 3, 6, HarvestTools.Pickaxe, HarvestRemnant.Gone),
        ResourceKind.IronOre => new HarvestSpec(
            ResourceKind.IronOre, "iron_ore", 2, 8, HarvestTools.Pickaxe, HarvestRemnant.Gone),
        ResourceKind.Sand => new HarvestSpec(
            ResourceKind.Sand, "sand", 4, 4, HarvestTools.Shovel, HarvestRemnant.RegrowNextShift),
        ResourceKind.Berries => new HarvestSpec(
            ResourceKind.Berries, "berries", 2, 2, HarvestTools.Hand, HarvestRemnant.RegrowNextShift),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static int YieldFor(ResourceKind kind, HarvestTool tool)
    {
        var spec = Of(kind);
        if (!spec.Allows(tool))
            return 0;
        if (tool == HarvestTool.Hand && kind == ResourceKind.Wood)
            return spec.YieldPerHit / 2;
        return spec.YieldPerHit;
    }
}
