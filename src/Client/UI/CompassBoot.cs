using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public static class CompassBoot
{
    public static CompassFrame Placeholder()
    {
        var tables = DebugWorld.Tables();
        return CompassFrame.From(tables, SpawnRing.CentreOf(WorldAtlas.FromTables(tables)));
    }
}
