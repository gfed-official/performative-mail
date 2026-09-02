using System;
using System.Collections.Generic;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Server;

public sealed class ServerRuntime
{
    private const int SnapshotChannel = 0;
    private const int HelloChannel = 2;

    private readonly ITransport _transport;
    private readonly List<byte[]> _pending = new();
    private uint _tick;
    private ClientSession? _session;

    public SimWorld World { get; }

    public ServerRuntime(ITransport transport)
        : this(transport, new SimWorld())
    {
    }

    public ServerRuntime(ITransport transport, SimWorld world)
    {
        _transport = transport;
        World = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void TickOnce()
    {
        DrainPending();
        HandleHellos();
        HandlePings();
        HandleInputs();

        World.Tick(_tick++);

        if (SnapshotCadence.ShouldSend(World.CurrentTick))
            SendSnapshot();
    }

    private void DrainPending()
    {
        _pending.Clear();
        while (_transport.Poll(out _, out var payload))
            _pending.Add(payload);
    }

    private void HandleHellos()
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            if (!WireCodec.TryDecode(_pending[i], out Hello hello))
                continue;

            HandleHello(in hello);
        }
    }

    private void HandleHello(in Hello hello)
    {
        if (_session is not null)
            return;

        if (hello.ProtocolHash != Protocol.Hash)
        {
            _transport.Send(HelloChannel, WireCodec.Encode(new HelloReject(HelloRejectReason.ProtocolMismatch)));
            return;
        }

        var body = World.Players.SpawnAtOrigin();
        _session = new ClientSession(body.Id);
        _transport.Send(HelloChannel, WireCodec.Encode(new HelloOk(body.Id, _tick)));
    }

    private void HandlePings()
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            if (!WireCodec.TryDecode(_pending[i], out Ping ping))
                continue;

            _transport.Send(SnapshotChannel, WireCodec.Encode(new Pong(ping.ClientStamp, _tick)));
        }
    }

    private void HandleInputs()
    {
        if (_session is null)
            return;

        for (int i = 0; i < _pending.Count; i++)
        {
            if (!WireCodec.TryDecode(_pending[i], out InputPacket? packet) || packet is null)
                continue;

            ApplyInputPacket(packet);
        }
    }

    private void ApplyInputPacket(InputPacket packet)
    {
        if (_session is null)
            return;
        if (!World.Players.TryGet(_session.Player, out var body))
            return;

        for (int i = packet.Commands.Count - 1; i >= 0; i--)
        {
            var cmd = packet.Commands[i];
            if (cmd.Tick > _tick)
                continue;
            if (body.HasAppliedInput && cmd.Tick <= body.LastProcessedInputTick)
                continue;

            World.ApplyInput(_session.Player, in cmd);
        }
    }

    private void SendSnapshot()
    {
        var players = new PlayerSnapshot[World.Players.Count];
        var all = World.Players.All;
        for (int i = 0; i < all.Count; i++)
        {
            var body = all[i];
            var lastProcessed = _session is not null && body.Id == _session.Player
                ? body.LastProcessedInputTick
                : 0u;
            players[i] = new PlayerSnapshot(
                body.Id,
                body.Xcm,
                body.Ycm,
                body.Zcm,
                body.Yaw,
                body.Anim,
                body.HpPct,
                lastProcessed);
        }

        _transport.Send(SnapshotChannel, WireCodec.Encode(new SnapshotPacket(World.CurrentTick, players)));
    }

    private sealed class ClientSession
    {
        public ClientSession(EntityId player) => Player = player;

        public EntityId Player { get; }
    }
}
