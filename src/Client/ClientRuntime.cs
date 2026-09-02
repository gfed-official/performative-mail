using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Client;

public sealed class ClientRuntime
{
    private const int InputChannel = 0;
    private const int HelloChannel = 2;
    private const int InputWindow = 3;

    private readonly List<InputCmd> _recent = new List<InputCmd>(InputWindow);

    public ITransport? Connection { get; private set; }

    public EntityId? LocalPlayer { get; private set; }

    public uint StartTick { get; private set; }

    public SnapshotPacket? LastSnapshot { get; private set; }

    public int SnapshotCount { get; private set; }

    public void Connect(ITransport transport)
    {
        Connection = transport;
        transport.Send(HelloChannel, WireCodec.Encode(new Hello(Protocol.Hash)));
    }

    public void SubmitInput(in InputCmd cmd)
    {
        if (_recent.Count == InputWindow)
            _recent.RemoveAt(InputWindow - 1);
        _recent.Insert(0, cmd);
    }

    public void TickOnce()
    {
        SendInputs();
        Receive();
    }

    public void Receive()
    {
        if (Connection is null)
            return;

        while (Connection.Poll(out _, out var payload))
            Apply(payload);
    }

    private void SendInputs()
    {
        if (Connection is null || _recent.Count == 0)
            return;

        Connection.Send(InputChannel, WireCodec.Encode(new InputPacket(_recent)));
    }

    private void Apply(byte[] payload)
    {
        if (!WireCodec.TryPeekKind(payload, out var kind))
            return;

        switch (kind)
        {
            case MessageKind.HelloOk:
                if (WireCodec.TryDecode(payload, out HelloOk helloOk))
                {
                    LocalPlayer = helloOk.LocalPlayer;
                    StartTick = helloOk.StartTick;
                }
                break;
            case MessageKind.Snapshot:
                if (WireCodec.TryDecode(payload, out SnapshotPacket? snapshot) && snapshot is not null)
                {
                    LastSnapshot = snapshot;
                    SnapshotCount++;
                }
                break;
            case MessageKind.Hello:
            case MessageKind.HelloReject:
            case MessageKind.Input:
                break;
            default:
                break;
        }
    }
}
