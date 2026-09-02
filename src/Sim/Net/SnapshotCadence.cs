namespace PerformativeMail.Sim.Net;

public static class SnapshotCadence
{
    public static bool ShouldSend(uint tick) => tick % 3 != 1;
}
