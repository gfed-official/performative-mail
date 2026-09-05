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
    InventoryEvent = 40,
    WorldOffer = 50,
    RunSettings = 51,
    JoinState = 52,
    AccountHello = 53,
    PlaceConstruct = 60,
    PlaceConstructConfirmed = 61,
    RemoveConstruct = 62,
    RemoveConstructConfirmed = 63,
    LaneInsert = 70,
    LaneRemove = 71,
    LaneChecksum = 72,
    LaneState = 73,
}
