using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Players;

public sealed class PlayerTable
{
    private readonly Dictionary<uint, PlayerBody> _byId = new Dictionary<uint, PlayerBody>();
    private readonly List<PlayerBody> _order = new List<PlayerBody>();
    private uint _nextCounter = 1;

    public int Count => _order.Count;

    public IReadOnlyList<PlayerBody> All => _order;

    public PlayerBody SpawnAtOrigin()
    {
        var id = EntityId.FromClassAndCounter(EntityClass.Player, _nextCounter++);
        var body = new PlayerBody(id);
        _byId.Add(id.Value, body);
        _order.Add(body);
        return body;
    }

    public bool TryGet(EntityId id, out PlayerBody body) => _byId.TryGetValue(id.Value, out body);
}
