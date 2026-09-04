using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class WorldHash
{
    public static ulong Compute(WorldTables tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));

        ulong hash = Fnv.Offset64;
        hash = Fnv.MixUInt32(hash, (uint)tables.Width);
        hash = Fnv.MixUInt32(hash, (uint)tables.Height);
        hash = Fnv.MixUInt32(hash, (uint)tables.TileCm);
        var heights = tables.Heights;
        for (int i = 0; i < heights.Length; i++)
            hash = Fnv.MixInt16(hash, heights[i]);
        hash = Fnv.MixUInt32(hash, (uint)tables.Addresses.Length);
        var addresses = tables.Addresses;
        for (int i = 0; i < addresses.Length; i++)
            hash = Fnv.MixUInt32(hash, addresses[i].Packed);

        var resources = (ResourceNodeRecord[])tables.ResourceNodes.Clone();
        Array.Sort(resources, (a, b) =>
        {
            int cmp = ((byte)a.Kind).CompareTo((byte)b.Kind);
            if (cmp != 0) return cmp;
            cmp = a.Tile.Y.CompareTo(b.Tile.Y);
            if (cmp != 0) return cmp;
            return a.Tile.X.CompareTo(b.Tile.X);
        });
        hash = Fnv.MixUInt32(hash, (uint)resources.Length);
        for (int i = 0; i < resources.Length; i++)
        {
            hash = Fnv.Mix8(hash, (byte)resources[i].Kind);
            hash = Fnv.MixUInt32(hash, (uint)resources[i].Tile.X);
            hash = Fnv.MixUInt32(hash, (uint)resources[i].Tile.Y);
        }

        var ferries = (FerryLaneRecord[])tables.Ferries.Clone();
        Array.Sort(ferries, (a, b) =>
        {
            int cmp = Pack(a.A).CompareTo(Pack(b.A));
            if (cmp != 0) return cmp;
            return Pack(a.B).CompareTo(Pack(b.B));
        });
        hash = Fnv.MixUInt32(hash, (uint)ferries.Length);
        for (int i = 0; i < ferries.Length; i++)
        {
            hash = Fnv.MixUInt32(hash, (uint)ferries[i].A.X);
            hash = Fnv.MixUInt32(hash, (uint)ferries[i].A.Y);
            hash = Fnv.MixUInt32(hash, (uint)ferries[i].B.X);
            hash = Fnv.MixUInt32(hash, (uint)ferries[i].B.Y);
        }

        var nodes = (RouteNodeRecord[])tables.RouteNodes.Clone();
        Array.Sort(nodes, (a, b) => a.Id.CompareTo(b.Id));
        hash = Fnv.MixUInt32(hash, (uint)nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
        {
            hash = Fnv.MixUInt32(hash, (uint)nodes[i].Id);
            hash = Fnv.MixUInt32(hash, (uint)nodes[i].Tile.X);
            hash = Fnv.MixUInt32(hash, (uint)nodes[i].Tile.Y);
        }

        var edges = (RouteEdgeRecord[])tables.RouteEdges.Clone();
        Array.Sort(edges, (a, b) =>
        {
            int cmp = a.From.CompareTo(b.From);
            if (cmp != 0) return cmp;
            return a.To.CompareTo(b.To);
        });
        hash = Fnv.MixUInt32(hash, (uint)edges.Length);
        for (int i = 0; i < edges.Length; i++)
        {
            hash = Fnv.MixUInt32(hash, (uint)edges[i].From);
            hash = Fnv.MixUInt32(hash, (uint)edges[i].To);
            hash = Fnv.MixUInt32(hash, (uint)edges[i].LengthTiles);
            hash = Fnv.Mix8(hash, edges[i].Surface);
        }

        var spawns = (SpawnEdgeRecord[])tables.SpawnEdges.Clone();
        Array.Sort(spawns, (a, b) =>
        {
            int cmp = a.District.CompareTo(b.District);
            if (cmp != 0) return cmp;
            cmp = a.Tile.Y.CompareTo(b.Tile.Y);
            if (cmp != 0) return cmp;
            return a.Tile.X.CompareTo(b.Tile.X);
        });
        hash = Fnv.MixUInt32(hash, (uint)spawns.Length);
        for (int i = 0; i < spawns.Length; i++)
        {
            hash = Fnv.Mix8(hash, spawns[i].District);
            hash = Fnv.MixUInt32(hash, (uint)spawns[i].Tile.X);
            hash = Fnv.MixUInt32(hash, (uint)spawns[i].Tile.Y);
            var path = spawns[i].PathToPo ?? Array.Empty<TileCoord>();
            hash = Fnv.MixUInt32(hash, (uint)path.Length);
            for (int p = 0; p < path.Length; p++)
            {
                hash = Fnv.MixUInt32(hash, (uint)path[p].X);
                hash = Fnv.MixUInt32(hash, (uint)path[p].Y);
            }
        }

        return hash;
    }

    private static int Pack(TileCoord tile) => (tile.X << 16) | (tile.Y & 0xFFFF);
}
