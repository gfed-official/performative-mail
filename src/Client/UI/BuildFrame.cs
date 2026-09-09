using System.Collections.Generic;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public readonly record struct BuildChoice(string Id, string Name, bool Selected);

public readonly record struct BuildCategoryTab(BuildCategory Category, string Label, bool Selected);

public readonly record struct BuildGhostHint(bool Valid, string Reason, TileCoord Tile);

public readonly record struct BuildFrame(
    bool Open,
    BuildCategory Category,
    string SelectedId,
    string SelectedName,
    Facing Facing,
    bool Valid,
    string Reason,
    IReadOnlyList<BuildCategoryTab> Categories,
    IReadOnlyList<BuildChoice> Choices);
