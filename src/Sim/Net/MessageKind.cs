namespace PerformativeMail.Sim.Net;

public enum MessageKind : byte
{
    Hello = 1,
    HelloOk = 2,
    HelloReject = 3,
    Input = 10,
    Snapshot = 20,
}
