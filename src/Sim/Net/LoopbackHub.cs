using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Net;

public sealed class LoopbackHub
{
    private readonly ITransport[] _serverEnds;
    private readonly ITransport[] _clientEnds;

    private LoopbackHub(ITransport[] serverEnds, ITransport[] clientEnds)
    {
        _serverEnds = serverEnds;
        _clientEnds = clientEnds;
    }

    public IReadOnlyList<ITransport> ServerEnds => _serverEnds;

    public IReadOnlyList<ITransport> ClientEnds => _clientEnds;

    public static LoopbackHub ForSeats(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "LoopbackHub needs at least one seat pair.");

        var serverEnds = new ITransport[count];
        var clientEnds = new ITransport[count];
        for (int i = 0; i < count; i++)
        {
            var pair = new LoopbackTransport();
            serverEnds[i] = pair.A;
            clientEnds[i] = pair.B;
        }

        return new LoopbackHub(serverEnds, clientEnds);
    }
}
