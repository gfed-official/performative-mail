using System;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class WorldResourcePlacement
{
    public const string NodePrefix = "Resource_";

    public static string NodeName(TileCoord tile) => NodePrefix + tile.X + "_" + tile.Y;

    public static string Label(ResourceKind kind, HarvestRemnant remnant) =>
        WorldResourceNames.MarkerLabel(kind, remnant);

    public static bool IsMarkerVisible(HarvestRemnant remnant) => remnant switch
    {
        HarvestRemnant.Live => true,
        HarvestRemnant.Stump => true,
        HarvestRemnant.Gone => false,
        HarvestRemnant.RegrowNextShift => false,
        _ => throw new ArgumentOutOfRangeException(nameof(remnant), remnant, null)
    };

    public static (float X, float Y, float Z) BoxSize(ResourceKind kind, HarvestRemnant remnant)
    {
        if (remnant == HarvestRemnant.Stump)
            return (0.50f, 0.40f, 0.50f);

        return kind switch
        {
            ResourceKind.Wood => (0.55f, 2.20f, 0.55f),
            ResourceKind.Fiber => (0.70f, 0.65f, 0.70f),
            ResourceKind.Stone => (0.85f, 0.70f, 0.85f),
            ResourceKind.IronOre => (0.70f, 0.55f, 0.70f),
            ResourceKind.Sand => (0.95f, 0.32f, 0.95f),
            ResourceKind.Berries => (0.65f, 0.70f, 0.65f),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static (float R, float G, float B) ColorRgb(ResourceKind kind, HarvestRemnant remnant)
    {
        if (remnant == HarvestRemnant.Stump)
            return (0.35f, 0.24f, 0.14f); // #593D24

        return kind switch
        {
            ResourceKind.Wood => (0.24f, 0.42f, 0.18f), // #3D6B2E
            ResourceKind.Fiber => (0.48f, 0.56f, 0.23f), // #7A8F3A
            ResourceKind.Stone => (0.54f, 0.53f, 0.50f), // #8A8680
            ResourceKind.IronOre => (0.42f, 0.23f, 0.20f), // #6B3A32
            ResourceKind.Sand => (0.77f, 0.65f, 0.42f), // #C4A66B
            ResourceKind.Berries => (0.55f, 0.18f, 0.29f), // #8C2F4A
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
