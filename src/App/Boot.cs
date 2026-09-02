using System;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.App;

public static class Boot
{
    public static (ServerRuntime Server, ClientRuntime Client, LoopbackTransport Transport) CreateListenHost()
        => CreateListenHost(new SimWorld());

    public static (ServerRuntime Server, ClientRuntime Client, LoopbackTransport Transport) CreateListenHost(SimWorld world)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));

        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(loopback.A, world);
        var client = new ClientRuntime();
        server.Start();
        client.Connect(loopback.B);
        return (server, client, loopback);
    }
}
