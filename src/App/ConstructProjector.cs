using PerformativeMail.Client;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class ConstructProjector
{
    public static ConstructFrame From(
        ConstructRegistry? registry,
        LaneReplica? lanes,
        float advanceDt)
    {
        if (registry is null || registry.Count == 0)
            return ConstructFrame.Empty with { TileCm = registry?.TileCm ?? ConstructFrame.Empty.TileCm };

        var all = registry.All;
        var placed = new ConstructView[all.Count];
        for (int i = 0; i < all.Count; i++)
        {
            var row = all[i];
            bool known = registry.TryGetBuilding(row.DefId, out var building);
            placed[i] = new ConstructView(
                row.Id.Value,
                row.DefId,
                known ? building.Name : row.DefId,
                known ? building.Behaviour : BuildingBehaviour.Wall,
                row.Tile,
                row.Rotation,
                known ? building.Footprint.W : (byte)1,
                known ? building.Footprint.H : (byte)1);
        }

        if (lanes is null)
            return new ConstructFrame(placed, Array.Empty<LaneItemView>(), registry.TileCm);

        var belts = new BeltNetwork();
        belts.Compile(all);
        var items = new List<LaneItemView>();
        for (int s = 0; s < belts.Segments.Count; s++)
        {
            var segment = belts.Segments[s];
            int lengthCm = BeltNetwork.PositionAtTickCm(segment.LengthMetres);
            if (advanceDt > 0f)
                lanes.Advance(segment.Id, advanceDt, BeltNetwork.Mk1MetresPerSecond, lengthCm);

            var tiles = CopyTiles(segment.Tiles);
            for (byte lane = 0; lane < BeltNetwork.LaneCount; lane++)
            {
                var positions = lanes.DrawPositions(segment.Id, lane, lengthCm);
                for (int i = 0; i < positions.Count; i++)
                    items.Add(new LaneItemView(segment.Id.Value, lane, positions[i], tiles, segment.Facing));
            }
        }

        return new ConstructFrame(placed, items, registry.TileCm);
    }

    private static TileCoord[] CopyTiles(IReadOnlyList<TileCoord> tiles)
    {
        var copy = new TileCoord[tiles.Count];
        for (int i = 0; i < tiles.Count; i++)
            copy[i] = tiles[i];
        return copy;
    }
}
