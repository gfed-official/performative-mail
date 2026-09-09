using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public readonly record struct ResourceNodeView(
    ResourceKind Kind,
    TileCoord Tile,
    HarvestRemnant Remnant,
    int HitsLeft);
