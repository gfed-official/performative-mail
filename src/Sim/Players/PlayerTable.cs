using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Players;

public sealed class PlayerTable
{
    private readonly Dictionary<uint, PlayerBody> _byId = new Dictionary<uint, PlayerBody>();
    private readonly List<PlayerBody> _order = new List<PlayerBody>();
    private readonly HashSet<uint> _usedSlots = new HashSet<uint>();
    private uint _nextCounter = 1;

    public int Count => _order.Count;

    public IReadOnlyList<PlayerBody> All => _order;

    public PlayerBody SpawnAtOrigin() => Spawn(PlayerPose.Origin);

    public PlayerBody SpawnOnRing(in PlayerPose centre)
    {
        uint slot = TakeSlot();
        return Add(new PlayerBody(
            EntityId.FromClassAndCounter(EntityClass.Player, _nextCounter++),
            SpawnRing.Pose(in centre, slot),
            slot));
    }

    public PlayerBody Spawn(in PlayerPose pose)
    {
        uint slot = TakeSlot();
        return Add(new PlayerBody(
            EntityId.FromClassAndCounter(EntityClass.Player, _nextCounter++),
            in pose,
            slot));
    }

    public bool Remove(EntityId id)
    {
        if (!_byId.TryGetValue(id.Value, out var body))
            return false;

        _byId.Remove(id.Value);
        _order.Remove(body);
        _usedSlots.Remove(body.SpawnSlot);
        return true;
    }

    public bool TryGet(EntityId id, out PlayerBody body) => _byId.TryGetValue(id.Value, out body);

    private uint TakeSlot()
    {
        uint slot = 0;
        while (_usedSlots.Contains(slot))
            slot++;
        _usedSlots.Add(slot);
        return slot;
    }

    private PlayerBody Add(PlayerBody body)
    {
        _byId.Add(body.Id.Value, body);
        _order.Add(body);
        return body;
    }
}
