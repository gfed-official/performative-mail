using PerformativeMail.Sim.Net;

namespace PerformativeMail.App;

public readonly record struct ListenResult(IServerLink Link, ITransport HostSeat);

public interface INetworkStack
{
    ListenResult Listen(ushort port, int maxPlayers);

    IClientLink Connect(JoinTarget target);
}
