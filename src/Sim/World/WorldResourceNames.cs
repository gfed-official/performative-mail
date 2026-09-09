using System;

namespace PerformativeMail.Sim.World;

public static class WorldResourceNames
{
    public static string Label(ResourceKind kind) => kind switch
    {
        ResourceKind.Wood => "Wood",
        ResourceKind.Fiber => "Fiber",
        ResourceKind.Stone => "Stone",
        ResourceKind.IronOre => "Iron Ore",
        ResourceKind.Sand => "Sand",
        ResourceKind.Berries => "Berries",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static string MarkerLabel(ResourceKind kind, HarvestRemnant remnant) =>
        remnant == HarvestRemnant.Stump ? "Stump" : Label(kind);
}
