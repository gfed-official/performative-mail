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
    public static int ToYawDegrees(this Facing facing)
    {
        switch (facing)
        {
            case Facing.North: return 0;
            case Facing.East: return 90;
            case Facing.South: return 180;
            case Facing.West: return 270;
            default:
                throw Failed(facing);
        }
    }

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

    public static string ToToken(this Facing facing)
    {
        switch (facing)
        {
            case Facing.North: return "north";
            case Facing.East: return "east";
            case Facing.South: return "south";
            case Facing.West: return "west";
            default:
                throw Failed(facing);
        }
    }

    public static TileCoord Step(this Facing facing, TileCoord from)
    {
        switch (facing)
        {
            case Facing.North: return new TileCoord(from.X, from.Y + 1);
            case Facing.East: return new TileCoord(from.X + 1, from.Y);
            case Facing.South: return new TileCoord(from.X, from.Y - 1);
            case Facing.West: return new TileCoord(from.X - 1, from.Y);
            default:
                throw Failed(facing);
        }
    }

    private static ArgumentOutOfRangeException Failed(Facing facing)
        => new(nameof(facing), facing, "Unhandled facing.");
}
