using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client;

public sealed class ClientRuntime
{
    private const int InputChannel = 0;
    private const int HelloChannel = 2;
    private const int InputWindow = 3;

    private readonly List<InputCmd> _recent = new List<InputCmd>(InputWindow);
    private readonly Dictionary<EntityId, PlayerReplication.RemoteInterpolated> _remotes =
        new Dictionary<EntityId, PlayerReplication.RemoteInterpolated>();
    private readonly PlayerReplication.OwnerPredicted _owner =
        new PlayerReplication.OwnerPredicted(new PredictionState());

    public ITransport? Connection { get; private set; }

    public EntityId? LocalPlayer { get; private set; }

    public uint StartTick { get; private set; }

    public uint ServerTickEstimate { get; private set; }

    public PlayerReplication.OwnerPredicted Owner => _owner;

    public PredictionState Prediction => _owner.State;

    public SnapshotPacket? LastSnapshot { get; private set; }

    public int SnapshotCount { get; private set; }

    public int RemoteCount => _remotes.Count;

    public Pong? LastPong { get; private set; }

    public HelloReject? LastReject { get; private set; }

    public WorldTables? GeneratedWorld { get; private set; }

    public ulong? AcceptedWorldHash { get; private set; }

    public RunSettings? AcceptedSettings { get; private set; }

    public JoinState? AcceptedJoin { get; private set; }

    public InventorySystem? Inventory { get; }

    public ConstructRegistry? Constructs { get; set; }

    public LaneReplica Lanes { get; } = new LaneReplica();

    public int InventoryEventCount { get; private set; }

    public ClientRuntime()
    {
    }

    public ClientRuntime(IStackCatalog catalog)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        Inventory = new InventorySystem(catalog);
    }

    public void Connect(ITransport transport) => Connect(transport, accountId: 0);

    public void Connect(ITransport transport, uint accountId)
    {
        Connection = transport;
        if (accountId != 0)
            transport.Send(HelloChannel, WireCodec.Encode(new AccountHello(accountId)));
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
                if (LastReject is null)
                    ApplySnapshot(payload);
                break;
            case MessageKind.Pong:
                ApplyPong(payload);
                break;
            case MessageKind.InventoryEvent:
                ApplyInventoryEvent(payload);
                break;
            case MessageKind.PlaceConstructConfirmed:
                ApplyPlaceConstruct(payload);
                break;
            case MessageKind.RemoveConstructConfirmed:
                ApplyRemoveConstruct(payload);
                break;
            case MessageKind.LaneInsert:
                ApplyLaneInsert(payload);
                break;
            case MessageKind.LaneRemove:
                ApplyLaneRemove(payload);
                break;
            case MessageKind.HelloReject:
                ApplyHelloReject(payload);
                break;
            case MessageKind.WorldOffer:
                ApplyWorldOffer(payload);
                break;
            case MessageKind.RunSettings:
                ApplyRunSettings(payload);
                break;
            case MessageKind.JoinState:
                ApplyJoinState(payload);
                break;
            case MessageKind.Hello:
            case MessageKind.AccountHello:
            case MessageKind.Input:
            case MessageKind.Ping:
            case MessageKind.PlaceConstruct:
            case MessageKind.RemoveConstruct:
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

    private void ApplyHelloReject(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out HelloReject reject))
            return;

        LastReject = reject;
    }

    private void ApplyRunSettings(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out RunSettings settings))
            return;

        AcceptedSettings = settings;
    }

    private void ApplyWorldOffer(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out WorldOffer offer))
            return;

        var verdict = WorldHashCheck.Accept(offer.Seed, offer.WorldHash, out var tables, out var hash);
        if (verdict != WorldHashVerdict.Match)
        {
            LastReject = new HelloReject(HelloRejectReason.VersionMismatch);
            GeneratedWorld = null;
            AcceptedWorldHash = null;
            return;
        }

        GeneratedWorld = tables;
        AcceptedWorldHash = hash;
    }

    private void ApplyJoinState(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out JoinState join))
            return;

        var verdict = WorldHashCheck.Accept(join.Seed, join.WorldHash, out var tables, out var hash);
        if (verdict != WorldHashVerdict.Match)
        {
            LastReject = new HelloReject(HelloRejectReason.VersionMismatch);
            GeneratedWorld = null;
            AcceptedWorldHash = null;
            AcceptedJoin = null;
            return;
        }

        GeneratedWorld = tables;
        AcceptedWorldHash = hash;
        AcceptedJoin = join;
    }

    private void ApplyPong(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out Pong pong))
            return;

        LastPong = pong;
    }

    private void ApplyInventoryEvent(byte[] payload)
    {
        if (Inventory is null)
            return;
        if (!InventoryCodec.TryParseEvent(payload, out var delta, out _))
            return;

        if (Inventory.ApplyDelta(delta) == ReplicaResult.Applied)
            InventoryEventCount++;
    }

    private void ApplyPlaceConstruct(byte[] payload)
    {
        if (Constructs is null)
            return;
        if (!ConstructCodec.TryDecode(payload, out PlaceConstructConfirmed placed))
            return;

        int hp = Constructs.TryGetBuilding(placed.BuildingId, out var building) ? building.Hp : 0;
        var record = new ConstructRecord(
            placed.ConstructId,
            placed.BuildingId,
            new TileCoord(placed.TileX, placed.TileY),
            placed.Rotation,
            placed.Owner,
            hp,
            hp);
        Constructs.TryApplyPlaced(record);
    }

    private void ApplyRemoveConstruct(byte[] payload)
    {
        if (Constructs is null)
            return;
        if (!ConstructCodec.TryDecode(payload, out RemoveConstructConfirmed removed))
            return;

        Constructs.TryApplyRemoved(removed.ConstructId);
    }

    private void ApplyLaneInsert(byte[] payload)
    {
        if (!LaneCodec.TryDecode(payload, out LaneInsert insert))
            return;

        Lanes.Apply(insert);
    }

    private void ApplyLaneRemove(byte[] payload)
    {
        if (!LaneCodec.TryDecode(payload, out LaneRemove remove))
            return;

        Lanes.Apply(remove);
    }

    private void ApplySnapshot(byte[] payload)
    {
        if (!WireCodec.TryDecode(payload, out SnapshotPacket? snapshot) || snapshot is null)
            return;

        LastSnapshot = snapshot;
        SnapshotCount++;
        TryReconcileOwner(snapshot);
        IngestRemotes(snapshot);
    }

    public bool TryGetRemote(EntityId id, out PlayerReplication.RemoteInterpolated remote) =>
        _remotes.TryGetValue(id, out remote!);

    public bool TryGetReplication(EntityId id, out PlayerReplication role)
    {
        if (LocalPlayer is EntityId owner && id == owner)
        {
            role = _owner;
            return true;
        }

        if (_remotes.TryGetValue(id, out var remote))
        {
            role = remote;
            return true;
        }

        role = null!;
        return false;
    }

    public PlayerReplication ReplicationFor(EntityId id)
    {
        if (TryGetReplication(id, out var role))
            return role;

        throw new InvalidOperationException($"No replication role for entity {id.Value}.");
    }

    public bool TryPresent(EntityId id, TimeSpan now, out PlayerPose pose)
    {
        if (!TryGetReplication(id, out var role))
        {
            pose = default;
            return false;
        }

        switch (role)
        {
            case PlayerReplication.OwnerPredicted owner:
                pose = owner.State.Pose;
                return true;
            case PlayerReplication.RemoteInterpolated remote:
                return remote.Buffer.TryPresent(now, out pose);
            default:
                throw new ArgumentOutOfRangeException(nameof(role), role, null);
        }
    }

    private void TryReconcileOwner(SnapshotPacket snapshot)
    {
        if (LocalPlayer is not EntityId local)
            return;
        if (!OwnerSnapshot.TryFrom(snapshot, local, out var owner))
            return;

        Prediction.Reconcile(in owner);
    }

    private void IngestRemotes(SnapshotPacket snapshot)
    {
        if (LocalPlayer is not EntityId owner)
            return;

        var seen = new HashSet<EntityId>();
        for (int i = 0; i < snapshot.Players.Count; i++)
        {
            var player = snapshot.Players[i];
            if (player.Id == owner)
                continue;

            seen.Add(player.Id);
            if (!_remotes.TryGetValue(player.Id, out var remote))
            {
                remote = new PlayerReplication.RemoteInterpolated(
                    InterpolationBuffer.ForRemote(player.Id, owner));
                _remotes.Add(player.Id, remote);
            }

            remote.Buffer.Push(RemoteSnapshot.From(in player, snapshot.ServerTick, owner));
        }

        if (_remotes.Count == seen.Count)
            return;

        var stale = new List<EntityId>();
        foreach (var id in _remotes.Keys)
        {
            if (!seen.Contains(id))
                stale.Add(id);
        }

        for (int i = 0; i < stale.Count; i++)
            _remotes.Remove(stale[i]);
    }
}
