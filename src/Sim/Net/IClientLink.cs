using System;

namespace PerformativeMail.Sim.Net;

public enum LinkState : byte
{
    Connecting = 1,
    Open = 2,
    Closed = 3,
}

public interface IClientLink : ITransport, IDisposable
{
    LinkState State { get; }

    DisconnectReason CloseReason { get; }

    void Pump();
}
