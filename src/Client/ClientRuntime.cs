using System;
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

    public uint ServerTickEstimate { get; private set; }

    public PredictionState Prediction { get; } = new PredictionState();

    public SnapshotPacket? LastSnapshot { get; private set; }

    public int SnapshotCount { get; private set; }

    public Pong? LastPong { get; private set; }

    public void Connect(ITransport transport)
    {
        Connection = transport;
        transport.Send(HelloChannel, WireCodec.Encode(new Hello(Protocol.Hash)));
    }

    public void SeedServerTickEstimate(uint tick) => ServerTickEstimate = tick;

    public void SendPing(uint stamp)
    {
        if (Connection is null)
            return;

        Connection.Send(InputChannel, WireCodec.Encode(new Ping(stamp)));
    }

    public void SubmitInput(in InputCmd cmd)
    {
        var stamped = new InputCmd(ServerTickEstimate, cmd.AxisX, cmd.AxisY, cmd.Yaw, cmd.Buttons);
        ServerTickEstimate++;

        if (_recent.Count == InputWindow)
            _recent.RemoveAt(InputWindow - 1);
        _recent.Insert(0, stamped);
        Prediction.Predict(in stamped);
    }

    public void TickOnce()
    {
        SendInputs();
        Receive();
    }

    public void SendInputs()
    {
        if (Connection is null || _recent.Count == 0)
            return;

        Connection.Send(InputChannel, WireCodec.Encode(new InputPacket(_recent)));
    }

    public void Receive()
    {
        if (Connection is null)
            return;

        while (Connection.Poll(out _, out var payload))
            Apply(payload);
    }

    private void Apply(byte[] payload)
    {
        if (!WireCodec.TryPeekKind(payload, out var kind))
            return;

        switch (kind)
        {
            case MessageKind.HelloOk:
                ApplyHelloOk(payload);
                break;
            case MessageKind.Snapshot:
                ApplySnapshot(payload);
                break;
            case MessageKind.Pong:
                ApplyPong(payload);
                break;
            case MessageKind.Hello:
            case MessageKind.HelloReject:
            case MessageKind.Input:
            case MessageKind.Ping:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private void ApplyHelloOk(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out HelloOk helloOk))
            return;

        LocalPlayer = helloOk.LocalPlayer;
        StartTick = helloOk.StartTick;
        if (Prediction.PendingCount == 0)
            ServerTickEstimate = helloOk.StartTick;
    }

    private void ApplyPong(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out Pong pong))
            return;

        LastPong = pong;
    }

    private void ApplySnapshot(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out SnapshotPacket? snapshot) || snapshot is null)
            return;

        LastSnapshot = snapshot;
        SnapshotCount++;
        TryReconcileOwner(snapshot);
    }

    private void TryReconcileOwner(SnapshotPacket snapshot)
    {
        if (LocalPlayer is not EntityId local)
            return;
        if (!OwnerSnapshot.TryFrom(snapshot, local, out var owner))
            return;

        Prediction.Reconcile(in owner);
    }
}
