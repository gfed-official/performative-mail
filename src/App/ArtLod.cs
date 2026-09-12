using System;

namespace PerformativeMail.App;

public enum ArtLodLevel : byte
{
    Lo0 = 0,
    Lo1 = 1,
    Lo2 = 2,
}

public static class ArtLod
{
    public const float Lo1Meters = 30f;
    public const float Lo2Meters = 60f;

    public static ArtLodLevel ForDistance(float meters)
    {
        if (meters < Lo1Meters)
            return ArtLodLevel.Lo0;
        if (meters < Lo2Meters)
            return ArtLodLevel.Lo1;
        return ArtLodLevel.Lo2;
    }

    public static string Path(string lo0Path, ArtLodLevel level)
    {
        switch (level)
        {
            case ArtLodLevel.Lo0:
                return lo0Path;
            case ArtLodLevel.Lo1:
                return WithLevelSuffix(lo0Path, 1);
            case ArtLodLevel.Lo2:
                return WithLevelSuffix(lo0Path, 2);
            default:
            {
                ArtLodLevel exhausted = level;
                throw new ArgumentOutOfRangeException(nameof(level), exhausted, null);
            }
        }
    }

    public static ArtLodLevel MaxLevel(string lo0Path)
    {
        string name = FileName(lo0Path);
        if (name.Equals("crate_01.glb", StringComparison.OrdinalIgnoreCase))
            return ArtLodLevel.Lo1;
        return IsLodAsset(name) ? ArtLodLevel.Lo2 : ArtLodLevel.Lo0;
    }

    public static ArtLodLevel Resolve(float meters, string lo0Path)
    {
        var wanted = ForDistance(meters);
        var max = MaxLevel(lo0Path);
        return wanted > max ? max : wanted;
    }

    // Missing LO1/LO2 files stay on the next available coarser mesh (LO0 if both absent).
    public static ArtLodLevel Fallback(ArtLodLevel wanted, bool hasLo1, bool hasLo2)
    {
        switch (wanted)
        {
            case ArtLodLevel.Lo0:
                return ArtLodLevel.Lo0;
            case ArtLodLevel.Lo1:
                return hasLo1 ? ArtLodLevel.Lo1 : ArtLodLevel.Lo0;
            case ArtLodLevel.Lo2:
                if (hasLo2)
                    return ArtLodLevel.Lo2;
                return hasLo1 ? ArtLodLevel.Lo1 : ArtLodLevel.Lo0;
            default:
            {
                ArtLodLevel exhausted = wanted;
                throw new ArgumentOutOfRangeException(nameof(wanted), exhausted, null);
            }
        }
    }

    public static float HorizontalMeters(float ax, float az, float bx, float bz)
    {
        float dx = ax - bx;
        float dz = az - bz;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static bool IsLodAsset(string fileName)
    {
        switch (fileName)
        {
            case "mailbox_01.glb":
            case "intake_01.glb":
            case "po_01.glb":
            case "house_a.glb":
            case "house_b.glb":
            case "house_c.glb":
            case "cart_01.glb":
            case "street_pole_01.glb":
                return true;
            default:
                return false;
        }
    }

    private static string WithLevelSuffix(string lo0Path, int level)
    {
        const string glb = ".glb";
        if (lo0Path.EndsWith(glb, StringComparison.OrdinalIgnoreCase))
            return lo0Path[..^glb.Length] + "_lo" + level + glb;
        return lo0Path + "_lo" + level;
    }

    private static string FileName(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path[(slash + 1)..] : path;
    }
}
