namespace PerformativeMail.Sim.World;

public enum WorldHashVerdict : byte
{
    Match = 0,
    VersionMismatch = 1,
}

public static class WorldHashCheck
{
    public static WorldTables Regenerate(uint seed) => WorldGen.GenerateSmallIsland(seed);

    public static WorldHashVerdict Compare(ulong local, ulong expected) =>
        local == expected ? WorldHashVerdict.Match : WorldHashVerdict.VersionMismatch;

    public static WorldHashVerdict Accept(uint seed, ulong expected, out WorldTables tables, out ulong local)
    {
        if (expected == DebugWorld.Hash)
        {
            tables = DebugWorld.Tables();
            local = WorldHash.Compute(tables);
            return Compare(local, expected);
        }

        tables = Regenerate(seed);
        local = WorldHash.Compute(tables);
        return Compare(local, expected);
    }
}
