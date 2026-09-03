namespace PerformativeMail.Sim.Net;

public readonly record struct ConnectionId(uint Value)
{
    public static readonly ConnectionId HostSeat = new(0);

    public bool IsHostSeat => Value == 0;
}
