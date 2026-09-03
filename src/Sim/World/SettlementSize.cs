using System;

namespace PerformativeMail.Sim.World;

public enum SettlementSize
{
    Small,
    Medium,
    Large,
    City
}

public readonly record struct PopulationBand(int MinHouses, int MaxHouses)
{
    public bool Contains(int houses) => houses >= MinHouses && houses <= MaxHouses;
}

public static class SettlementBands
{
    public static PopulationBand For(SettlementSize size) => size switch
    {
        SettlementSize.Small => new PopulationBand(8, 12),
        SettlementSize.Medium => new PopulationBand(13, 25),
        SettlementSize.Large => new PopulationBand(26, 50),
        SettlementSize.City => new PopulationBand(100, 200),
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown settlement size.")
    };

    public static PopulationBand Grown(SettlementSize size)
    {
        var start = For(size);
        int max = size == SettlementSize.Medium ? For(SettlementSize.Large).MaxHouses : start.MaxHouses;
        return new PopulationBand(start.MinHouses, max);
    }

    public static bool TryParse(string? id, out SettlementSize size)
    {
        switch (id)
        {
            case "small":
                size = SettlementSize.Small;
                return true;
            case "medium":
                size = SettlementSize.Medium;
                return true;
            case "large":
                size = SettlementSize.Large;
                return true;
            case "city":
                size = SettlementSize.City;
                return true;
            default:
                size = default;
                return false;
        }
    }
}
