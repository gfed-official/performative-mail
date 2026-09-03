using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.World;

internal static class ValidationStage
{
    public const int MinHouses = 8;
    public const int MinPadTiles = 400;
    public const int PadChebyshev = 20;
    public const int DeepCm = -200;
    public const int DeepRangeTiles = 100;

    public static bool Evaluate(WorldTables tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        if (!HeightmapStage.MeetsBuildableQuota(tables.Heights, tables.Buildable))
            return false;
        if (tables.Houses.Length < MinHouses)
            return false;
        if (tables.Addresses.Length != tables.Houses.Length)
            return false;
        var seenAddr = new HashSet<uint>(tables.Addresses.Length);
        for (int i = 0; i < tables.Addresses.Length; i++)
        {
            if (!seenAddr.Add(tables.Addresses[i].Packed))
                return false;
        }

        for (int i = 0; i < tables.Houses.Length; i++)
        {
            if (!seenAddr.Contains(tables.Houses[i].Address.Packed))
                return false;
        }

        if (!MailboxesReachable(tables))
            return false;
        if (CountPadTiles(tables) < MinPadTiles)
            return false;
        if (!HasDeepWater(tables))
            return false;
        if (!ResourceMins(tables))
            return false;
        if (tables.SpawnEdges.Length < 1)
            return false;
        if (tables.Streets.Length > 0 &&
            (tables.RouteNodes.Length == 0 || tables.RouteEdges.Length == 0))
            return false;
        return true;
    }

    private static bool MailboxesReachable(WorldTables tables)
    {
        int width = tables.Width;
        int height = tables.Height;
        int count = width * height;
        var walk = new bool[count];
        WorldGrid.FillWalkable(
            walk,
            tables.Heights,
            tables.Buildable,
            width,
            height,
            tables.PostOffice,
            tables.Streets);
        var seen = new bool[count];
        var parent = new int[count];
        int spawn = WorldGrid.WalkStart(tables.PostOffice, walk, width, height);
        WorldGrid.FerryPairs(tables.Ferries, width, height, out int[] ferryFrom, out int[] ferryTo);
        WorldGrid.BfsWalk(
            walk,
            tables.Heights,
            width,
            height,
            spawn,
            seen,
            parent,
            ferryFrom,
            ferryTo);

        for (int i = 0; i < tables.Houses.Length; i++)
        {
            var m = tables.Houses[i].Mailbox.Tile(tables.TileCm);
            if (!WorldGrid.InBounds(m.X, m.Y, width, height))
                return false;
            if (!seen[WorldGrid.Idx(m.X, m.Y, width)])
                return false;
        }

        return true;
    }

    private static int CountPadTiles(WorldTables tables)
    {
        int width = tables.Width;
        int height = tables.Height;
        var lots = new bool[width * height];
        WorldGrid.FillLotMask(lots, width, height, tables.Lots);
        int pcx = tables.PostOffice.Tile.X + tables.PostOffice.SizeTiles.X / 2;
        int pcy = tables.PostOffice.Tile.Y + tables.PostOffice.SizeTiles.Y / 2;
        int n = 0;
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (WorldGrid.Chebyshev(x, y, pcx, pcy) > PadChebyshev) continue;
                int i = row + x;
                if (!HeightmapStage.IsLand(tables.Heights[i]) || !tables.Buildable[i]) continue;
                if (lots[i]) continue;
                n++;
            }
        }

        return n;
    }

    private static bool HasDeepWater(WorldTables tables)
    {
        int width = tables.Width;
        int pcx = tables.PostOffice.Tile.X + tables.PostOffice.SizeTiles.X / 2;
        int pcy = tables.PostOffice.Tile.Y + tables.PostOffice.SizeTiles.Y / 2;
        int rangeSq = DeepRangeTiles * DeepRangeTiles;
        for (int y = 0; y < tables.Height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (tables.Heights[row + x] > DeepCm) continue;
                if (WorldGrid.DistSq(x, y, pcx, pcy) <= rangeSq)
                    return true;
            }
        }

        return false;
    }

    private static bool ResourceMins(WorldTables tables)
    {
        int wood = 0, fiber = 0, stone = 0, ore = 0, sand = 0, berries = 0;
        var nodes = tables.ResourceNodes;
        for (int i = 0; i < nodes.Length; i++)
        {
            switch (nodes[i].Kind)
            {
                case ResourceKind.Wood: wood++; break;
                case ResourceKind.Fiber: fiber++; break;
                case ResourceKind.Stone: stone++; break;
                case ResourceKind.IronOre: ore++; break;
                case ResourceKind.Sand: sand++; break;
                case ResourceKind.Berries: berries++; break;
            }
        }

        return wood >= ResourceStage.WoodMin
            && fiber >= ResourceStage.FiberMin
            && stone >= ResourceStage.StoneMin
            && ore >= ResourceStage.IronOreMin
            && sand >= ResourceStage.SandMin
            && berries >= ResourceStage.BerriesMin;
    }
}
