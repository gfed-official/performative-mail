namespace PerformativeMail.Sim.Net;

public enum MessageKind : byte
{
    Hello = 1,
    HelloOk = 2,
    HelloReject = 3,
    Input = 10,
    Snapshot = 20,
    Ping = 30,
    Pong = 31,
    // Channel 1. Next free after Hello/Input/Snapshot/Ping. Do not renumber the above.
    InventoryEvent = 40,
    WorldOffer = 50,
}
