using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Combat;

public sealed class EnemyTable
{
    private readonly Dictionary<uint, EnemyAgent> _byId = new Dictionary<uint, EnemyAgent>();
    private readonly List<EnemyAgent> _order = new List<EnemyAgent>();
    private readonly RoutingGraph _graph;
    private readonly TileCoord _pad;
    private readonly int _tileCm;
    private uint _nextCounter = 1;

    public EnemyTable(RoutingGraph graph, PostOfficeRecord postOffice, int tileCm = WorldAtlas.TileCmDefault)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm), tileCm, null);
        _tileCm = tileCm;
        _pad = postOffice.SpawnPadTile;
        if (!_graph.TryNearestNode(_pad, out _))
            throw new ArgumentException("Routing graph has no nodes.", nameof(graph));
    }

    public int Count => _order.Count;

    public IReadOnlyList<EnemyAgent> All => _order;

    public EnemyAgent Spawn(EnemyKind kind, TileCoord at)
    {
        if (!_graph.TryPath(at, _pad, out var path))
            throw new ArgumentException($"No route from {at} to the Post Office pad.", nameof(at));

        var stats = EnemyStats.ForKind(kind);
        var id = EntityId.FromClassAndCounter(EntityClass.Enemy, _nextCounter++);
        var agent = new EnemyAgent(id, kind, stats, path, new PathCursor(path.Tiles, _tileCm));
        _byId.Add(id.Value, agent);
        _order.Add(agent);
        return agent;
    }

    public bool TryGet(EntityId id, out EnemyAgent agent) => _byId.TryGetValue(id.Value, out agent);

    public void Step(float dt)
    {
        if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt), dt, null);
        for (int i = 0; i < _order.Count; i++)
            _order[i].Step(dt);
    }
}
