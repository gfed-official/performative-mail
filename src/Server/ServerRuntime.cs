using System;
using System.Collections.Generic;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Server;

public sealed class ServerRuntime
{
    private readonly IServerLink _link;
    private readonly Dictionary<ConnectionId, Seat> _seats = new();
    private PlayerSnapshot[] _snapshotScratch = Array.Empty<PlayerSnapshot>();
    private uint _tick;

    public SimWorld World { get; }

    public IReadOnlyList<ContainerDelta> LastFlushedDeltas { get; private set; } = Array.Empty<ContainerDelta>();

    public int JoinedCount
    {
        get
        {
            int n = 0;
            foreach (var seat in _seats.Values)
            {
                if (seat.Joined)
                    n++;
            }

            return n;
        }
    }

    public ServerRuntime(IServerLink link)
        : this(link, new SimWorld())
    {
    }

    public ServerRuntime(IServerLink link, SimWorld world)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
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
        Drain();
        World.Tick(_tick++);
        FlushInventoryEvents();

        if (SnapshotCadence.ShouldSend(World.CurrentTick))
            Broadcast();
    }

    private void Drain()
    {
        while (_link.TryPoll(out var ev))
        {
            switch (ev.Kind)
            {
                case LinkEventKind.Opened:
                    if (!_seats.ContainsKey(ev.Connection))
                        _seats[ev.Connection] = new Seat(ev.Connection, null);
                    break;
                case LinkEventKind.Data:
                    OnData(ev.Connection, ev.Payload);
                    break;
                case LinkEventKind.Closed:
                    OnClosed(ev.Connection);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ev.Kind), ev.Kind, null);
            }
        }
    }

    private void OnData(ConnectionId from, byte[] payload)
    {
        if (!_seats.ContainsKey(from))
            return;

        if (WireCodec.TryDecode(payload, out Hello hello))
        {
            HandleHello(from, in hello);
            return;
        }

        if (WireCodec.TryDecode(payload, out Ping ping))
        {
            _link.Send(from, NetChannels.Unreliable, WireCodec.Encode(new Pong(ping.ClientStamp, _tick)));
            return;
        }

        if (WireCodec.TryDecode(payload, out InputPacket? packet) && packet is not null)
            ApplyInputPacket(from, packet);
    }

    private void HandleHello(ConnectionId from, in Hello hello)
    {
        var seat = _seats[from];
        if (seat.Player is not null)
            return;

        if (hello.ProtocolHash != Protocol.Hash)
        {
            _link.Send(from, NetChannels.Handshake, WireCodec.Encode(new HelloReject(HelloRejectReason.ProtocolMismatch)));
            _link.Close(from, DisconnectReason.Rejected);
            return;
        }

        var body = World.SpawnPlayer();
        _seats[from] = new Seat(from, body.Id);
        _link.Send(from, NetChannels.Handshake, WireCodec.Encode(new HelloOk(body.Id, _tick)));
    }

    private void ApplyInputPacket(ConnectionId from, InputPacket packet)
    {
        if (!_seats.TryGetValue(from, out var seat) || seat.Player is not EntityId player)
            return;
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

    private void OnClosed(ConnectionId from)
    {
        if (!_seats.TryGetValue(from, out var seat))
            return;

        _seats.Remove(from);
        if (seat.Player is EntityId player)
            World.Players.Remove(player);
    }

    private void Broadcast()
    {
        var all = World.Players.All;
        if (_snapshotScratch.Length != all.Count)
            _snapshotScratch = new PlayerSnapshot[all.Count];

        foreach (var seat in _seats.Values)
        {
            if (!seat.Joined)
                continue;

            for (int i = 0; i < all.Count; i++)
            {
                var body = all[i];
                var lastProcessed = seat.Player is EntityId player && body.Id == player
                    ? body.LastProcessedInputTick
                    : 0u;
                _snapshotScratch[i] = new PlayerSnapshot(
                    body.Id,
                    body.Xcm,
                    body.Ycm,
                    body.Zcm,
                    body.Yaw,
                    body.Anim,
                    body.HpPct,
                    lastProcessed);
            }

            _link.Send(
                seat.Id,
                NetChannels.Unreliable,
                WireCodec.Encode(new SnapshotPacket(World.CurrentTick, _snapshotScratch)));
        }
    }

    private void FlushInventoryEvents()
    {
        if (World.Inventory is not InventorySystem inventory)
        {
            LastFlushedDeltas = Array.Empty<ContainerDelta>();
            return;
        }

        var deltas = inventory.DrainCommittedDeltas();
        LastFlushedDeltas = deltas;
        for (int i = 0; i < deltas.Count; i++)
        {
            var delta = deltas[i];
            var payload = InventoryCodec.EncodeEvent(delta);
            foreach (var viewer in inventory.ViewersOf(delta.Container))
                SendToViewer(viewer, NetChannels.Reliable, payload);
        }
    }

    private void SendToViewer(EntityId viewer, int channel, byte[] payload)
    {
        foreach (var seat in _seats.Values)
        {
            if (seat.Player is EntityId player && player.Equals(viewer))
            {
                _link.Send(seat.Id, channel, payload);
                return;
            }
        }
    }

    private readonly struct Seat
    {
        public Seat(ConnectionId id, EntityId? player)
        {
            Id = id;
            Player = player;
        }

        public ConnectionId Id { get; }

        public EntityId? Player { get; }

        public bool Joined => Player.HasValue;
    }
}
