using System;
using PerformativeMail.Sim.Building;

namespace PerformativeMail.Client.UI;

public static class BuildRejectText
{
    public const string OutOfRange = "Out of range";

    public static string Of(PlaceReject reject) => reject switch
    {
        PlaceReject.UnknownBuilding => "Unknown building",
        PlaceReject.UnknownRecipe => "Unknown recipe",
        PlaceReject.OutOfBounds => "Out of bounds",
        PlaceReject.Water => "Needs deep water",
        PlaceReject.Street => "On street",
        PlaceReject.Slope => "Too steep",
        PlaceReject.Occupied => "Occupied",
        PlaceReject.UnknownItem => "Unknown item",
        PlaceReject.MissingInput => "Missing input",
        _ => throw new ArgumentOutOfRangeException(nameof(reject), reject, null),
    };
}
