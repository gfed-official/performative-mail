using PerformativeMail.Sim;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Server;

public sealed class ServerRuntime
{
    private readonly ITransport _transport;
    private uint _tick;

    public SimWorld World { get; }

    public ServerRuntime(ITransport transport)
    {
        _transport = transport;
        World = new SimWorld();
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void TickOnce()
    {
        while (_transport.Poll(out _, out _))
        {
        }

        World.Tick(_tick++);
    }
}
