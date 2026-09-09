using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public readonly record struct ConstructView(
    uint Id,
    string DefId,
    string Name,
    BuildingBehaviour Behaviour,
    TileCoord Tile,
    Facing Rotation,
    byte FootprintW,
    byte FootprintH);

public readonly record struct LaneItemView(
    ulong Segment,
    byte Lane,
    int PositionCm,
    IReadOnlyList<TileCoord> Tiles,
    Facing Facing);

public readonly record struct ConstructFrame(
    IReadOnlyList<ConstructView> Placed,
    IReadOnlyList<LaneItemView> LaneItems,
    int TileCm)
{
    public static ConstructFrame Empty { get; } =
        new(Array.Empty<ConstructView>(), Array.Empty<LaneItemView>(), 200);

    public int Stamp()
    {
        int hash = Placed.Count * 17 + LaneItems.Count;
        for (int i = 0; i < Placed.Count; i++)
            hash = (hash * 31) ^ (int)Placed[i].Id;
        for (int i = 0; i < LaneItems.Count; i++)
            hash = (hash * 31) ^ LaneItems[i].PositionCm ^ (LaneItems[i].Lane << 16);
        return hash;
    }
}
