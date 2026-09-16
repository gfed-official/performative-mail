using PerformativeMail.Sim;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.Soak;

public static class PackedFactory
{
    public const int ItemCount = 5000;

    public static int Fill(SimWorld world)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));

        int slotsPerLane = (int)(BeltNetwork.TileMetres / BeltNetwork.MinSpacingMetres);
        int tiles = (int)Math.Ceiling(
            ItemCount / (double)(BeltNetwork.LaneCount * slotsPerLane));
        var constructs = new ConstructRecord[tiles];
        for (int i = 0; i < tiles; i++)
        {
            constructs[i] = new ConstructRecord(
                EntityId.FromClassAndCounter(EntityClass.Construct, (uint)(i + 1)),
                BeltNetwork.BuildingId,
                new TileCoord(i, 0),
                Facing.East,
                default,
                80,
                80);
        }

        world.Belts.Compile(constructs);
        if (world.Belts.Segments.Count != 1)
            throw new InvalidOperationException($"Expected 1 segment, got {world.Belts.Segments.Count}.");
        var segment = world.Belts.Segments[0];
        int perLane = ItemCount / BeltNetwork.LaneCount;
        float spacing = BeltNetwork.MinSpacingMetres;
        int id = 1;
        for (int lane = 0; lane < BeltNetwork.LaneCount; lane++)
        {
            for (int i = 0; i < perLane; i++)
            {
                if (!segment.TryInsert(lane, id, i * spacing))
                    throw new InvalidOperationException($"Insert failed lane={lane} metres={i * spacing}.");
                id++;
            }
        }

        world.Belts.DrainLaneDeltas();
        return CountItems(world.Belts);
    }

    public static int CountItems(BeltNetwork belts)
    {
        if (belts is null)
            throw new ArgumentNullException(nameof(belts));

        int n = 0;
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            var segment = belts.Segments[i];
            n += segment.Lane(0).Count;
            n += segment.Lane(1).Count;
        }

        return n;
    }
}
