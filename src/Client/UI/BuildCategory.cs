using System;
using PerformativeMail.Sim.Content;

namespace PerformativeMail.Client.UI;

public enum BuildCategory : byte
{
    Transport,
    Sorting,
    Storage,
    Vehicles,
    Defense,
    Extractors
}

public static class BuildCategories
{
    public static string Label(BuildCategory category) => category switch
    {
        BuildCategory.Transport => "Transport",
        BuildCategory.Sorting => "Sorting",
        BuildCategory.Storage => "Storage",
        BuildCategory.Vehicles => "Vehicles",
        BuildCategory.Defense => "Defense",
        BuildCategory.Extractors => "Extractors",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    public static BuildCategory Of(BuildingBehaviour behaviour) => behaviour switch
    {
        BuildingBehaviour.Belt => BuildCategory.Transport,
        BuildingBehaviour.Pipe => BuildCategory.Transport,
        BuildingBehaviour.Splitter => BuildCategory.Transport,
        BuildingBehaviour.Merger => BuildCategory.Transport,
        BuildingBehaviour.Sorter => BuildCategory.Sorting,
        BuildingBehaviour.Inserter => BuildCategory.Sorting,
        BuildingBehaviour.Container => BuildCategory.Storage,
        BuildingBehaviour.Wall => BuildCategory.Defense,
        BuildingBehaviour.Gate => BuildCategory.Defense,
        BuildingBehaviour.Spike => BuildCategory.Defense,
        BuildingBehaviour.Turret => BuildCategory.Defense,
        BuildingBehaviour.Alarm => BuildCategory.Defense,
        BuildingBehaviour.VehicleDepot => BuildCategory.Storage,
        BuildingBehaviour.Port => BuildCategory.Vehicles,
        BuildingBehaviour.Pier => BuildCategory.Vehicles,
        BuildingBehaviour.Pump => BuildCategory.Extractors,
        _ => throw new ArgumentOutOfRangeException(nameof(behaviour), behaviour, null),
    };
}
