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

    private readonly List<ClientSession> _sessions = new();
    private readonly List<byte[]> _pending = new();
    private uint _tick;

    public SimWorld World { get; }

    public ServerRuntime(ITransport transport)
        : this(transport, new SimWorld())
    {
    }

    public ServerRuntime(ITransport transport, SimWorld world)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Attach(transport);
    }

    public void Attach(ITransport transport)
    {
        if (transport is null)
            throw new ArgumentNullException(nameof(transport));

        for (int i = 0; i < _sessions.Count; i++)
        {
            if (ReferenceEquals(_sessions[i].Transport, transport))
                throw new ArgumentException("Transport is already attached.", nameof(transport));
        }

        _sessions.Add(new ClientSession(transport));
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void TickOnce()
    {
        for (int i = 0; i < _sessions.Count; i++)
            Service(_sessions[i]);

        World.Tick(_tick++);

        if (SnapshotCadence.ShouldSend(World.CurrentTick))
        {
            for (int i = 0; i < _sessions.Count; i++)
                SendSnapshot(_sessions[i]);
        }
    }

    private void Service(ClientSession session)
    {
        _pending.Clear();
        while (session.Transport.Poll(out _, out var payload))
            _pending.Add(payload);

        HandleHellos(session);
        HandlePings(session);
        HandleInputs(session);
    }

    private void HandleHellos(ClientSession session)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            if (!WireCodec.TryDecode(_pending[i], out Hello hello))
                continue;

            HandleHello(session, in hello);
        }
    }

    private void HandleHello(ClientSession session, in Hello hello)
    {
        if (session.Player is not null)
            return;

        if (hello.ProtocolHash != Protocol.Hash)
        {
            session.Transport.Send(HelloChannel, WireCodec.Encode(new HelloReject(HelloRejectReason.ProtocolMismatch)));
            return;
        }

        var body = World.Players.SpawnAtOrigin();
        session.Player = body.Id;
        session.Transport.Send(HelloChannel, WireCodec.Encode(new HelloOk(body.Id, _tick)));
    }

    private void HandlePings(ClientSession session)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            if (!WireCodec.TryDecode(_pending[i], out Ping ping))
                continue;

            session.Transport.Send(SnapshotChannel, WireCodec.Encode(new Pong(ping.ClientStamp, _tick)));
        }
    }

    private void HandleInputs(ClientSession session)
    {
        if (session.Player is not EntityId player)
            return;

        for (int i = 0; i < _pending.Count; i++)
        {
            if (!WireCodec.TryDecode(_pending[i], out InputPacket? packet) || packet is null)
                continue;

            ApplyInputPacket(player, packet);
        }
    }

    private void ApplyInputPacket(EntityId player, InputPacket packet)
    {
        if (!World.Players.TryGet(player, out var body))
            return;

        for (int i = packet.Commands.Count - 1; i >= 0; i--)
        {
            var cmd = packet.Commands[i];
            if (cmd.Tick > _tick)
                continue;
            if (body.HasAppliedInput && cmd.Tick <= body.LastProcessedInputTick)
                continue;

            World.ApplyInput(player, in cmd);
        }
    }

    private void SendSnapshot(ClientSession session)
    {
        var players = new PlayerSnapshot[World.Players.Count];
        var all = World.Players.All;
        for (int i = 0; i < all.Count; i++)
        {
            var body = all[i];
            var lastProcessed = session.Player is EntityId player && body.Id == player
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

        session.Transport.Send(SnapshotChannel, WireCodec.Encode(new SnapshotPacket(World.CurrentTick, players)));
    }

    private sealed class ClientSession
    {
        public ClientSession(ITransport transport) => Transport = transport;

        public ITransport Transport { get; }

        public EntityId? Player { get; set; }
    }
}
