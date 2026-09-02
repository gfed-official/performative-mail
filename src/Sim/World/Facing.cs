using System;

namespace PerformativeMail.Sim.World;

public enum Facing : byte
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

public static class FacingConversions
{
    public static Facing FromYawDegrees(int yaw)
    {
        var normalized = yaw % 360;
        if (normalized < 0) normalized += 360;
        switch (normalized)
        {
            case 0: return Facing.North;
            case 90: return Facing.East;
            case 180: return Facing.South;
            case 270: return Facing.West;
            default:
                throw new WorldAtlasException($"Yaw {yaw} is not a cardinal heading.");
        }
    }

    public static Facing Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new WorldAtlasException("Facing is required.");

        switch (text.Trim().ToLowerInvariant())
        {
            case "north": return Facing.North;
            case "east": return Facing.East;
            case "south": return Facing.South;
            case "west": return Facing.West;
            default:
                throw new WorldAtlasException($"Unknown facing '{text}'.");
        }
    }
}
