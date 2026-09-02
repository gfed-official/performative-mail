using PerformativeMail.Sim.Net;

namespace PerformativeMail.Client;

public sealed class ClientRuntime
{
    public ITransport? Connection { get; private set; }

    public void Connect(ITransport transport)
    {
        Connection = transport;
    }
}
