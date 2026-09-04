using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Players;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Run;

public readonly record struct DeathBag(
    EntityId Owner,
    ContainerId Container,
    ContainerId? Overflow,
    TileCoord Tile,
    uint DespawnTick);

public sealed class DeathSession
{
    public static readonly int RespawnTicks = TickClock.TicksFromSeconds(10);

    public static readonly int DespawnTicks = TickClock.TicksFromSeconds(30);

    private static readonly ContainerSpec OverflowSpec = new(ContainerShape.Grid(20, 16), null);

    private readonly InventorySystem _inventory;
    private readonly ContainerId _intake;
    private readonly PlayerPose _respawnPose;
    private readonly Dictionary<uint, BoundPlayer> _bound = new();
    private readonly Dictionary<uint, uint> _respawnAt = new();
    private readonly List<DeathBag> _bags = new();

    public DeathSession(InventorySystem inventory, ContainerId intake, PlayerPose respawnPose)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _intake = intake;
        _respawnPose = respawnPose;
    }

    public uint Now { get; private set; }

    public void Bind(
        PlayerBody body,
        ContainerId hotbar,
        ContainerId inventory,
        ContainerId? backpack = null,
        ContainerId? cursor = null)
    {
        if (body is null) throw new ArgumentNullException(nameof(body));
        _bound[body.Id.Value] = new BoundPlayer(body, hotbar, inventory, backpack, cursor);
    }

    public bool Die(EntityId player, TileCoord tile, uint now)
    {
        if (!_bound.TryGetValue(player.Value, out var bound))
            return false;
        if (_respawnAt.ContainsKey(player.Value))
            return false;

        var stacks = new List<Stack>();
        Drain(bound.Hotbar, stacks);
        Drain(bound.Inventory, stacks);
        if (bound.Backpack is { } pack)
            Drain(pack, stacks);
        if (bound.Cursor is { } cursor)
            Drain(cursor, stacks);

        var bagId = _inventory.CreateContainer(ContainerSpec.DeathBag);
        ContainerId? overflow = null;
        foreach (var stack in stacks)
        {
            if (TryDeposit(bagId, stack))
                continue;
            overflow ??= _inventory.CreateContainer(OverflowSpec);
            if (!TryDeposit(overflow.Value, stack))
                throw new InvalidOperationException("Death bag overflow rejected a stack.");
        }

        if (now > Now)
            Now = now;
        _respawnAt[player.Value] = now + (uint)RespawnTicks;
        _bags.Add(new DeathBag(player, bagId, overflow, tile, now + (uint)DespawnTicks));
        return true;
    }

    public void AdvanceTo(uint tick)
    {
        if (tick < Now)
            throw new ArgumentOutOfRangeException(nameof(tick), tick, null);

        Now = tick;
        RespawnReady();
        DespawnReady();
    }

    public bool IsDead(EntityId player) => _respawnAt.ContainsKey(player.Value);

    public bool TryGetRespawnTick(EntityId player, out uint tick)
        => _respawnAt.TryGetValue(player.Value, out tick);

    public bool TryGetBag(EntityId player, out DeathBag bag)
    {
        for (int i = _bags.Count - 1; i >= 0; i--)
        {
            if (!_bags[i].Owner.Equals(player)) continue;
            bag = _bags[i];
            return true;
        }

        bag = default;
        return false;
    }

    public IReadOnlyList<Stack> StacksIn(in DeathBag bag)
    {
        var stacks = new List<Stack>();
        CopyStacks(bag.Container, stacks);
        if (bag.Overflow is { } overflow)
            CopyStacks(overflow, stacks);
        return stacks;
    }

    private void RespawnReady()
    {
        var done = new List<uint>();
        foreach (var pair in _respawnAt)
        {
            if (Now < pair.Value) continue;
            if (_bound.TryGetValue(pair.Key, out var bound))
                bound.Body.SetPose(_respawnPose);
            done.Add(pair.Key);
        }

        foreach (var id in done)
            _respawnAt.Remove(id);
    }

    private void DespawnReady()
    {
        for (int i = _bags.Count - 1; i >= 0; i--)
        {
            var bag = _bags[i];
            if (Now < bag.DespawnTick) continue;
            ReturnToIntake(bag);
            _bags.RemoveAt(i);
        }
    }

    private void ReturnToIntake(in DeathBag bag)
    {
        var stacks = new List<Stack>();
        stacks.AddRange(_inventory.DestroyContainer(bag.Container));
        if (bag.Overflow is { } overflow)
            stacks.AddRange(_inventory.DestroyContainer(overflow));

        foreach (var stack in stacks)
        {
            if (!TryDeposit(_intake, stack))
                throw new InvalidOperationException("Intake rejected a death-bag stack.");
        }
    }

    private void Drain(ContainerId container, List<Stack> into)
    {
        if (!_inventory.TryGetContainer(container, out var grid))
            return;

        var ids = new List<EntryId>(grid.Entries.Count);
        foreach (var entry in grid.Entries)
            ids.Add(entry.Id);

        foreach (var id in ids)
        {
            var result = _inventory.Apply(Actor.System, new Withdraw(container, id));
            if (result is not Accepted accepted || accepted.Withdrawn is null)
                throw new InvalidOperationException("System withdraw from a player container failed.");
            into.Add(accepted.Withdrawn);
        }
    }

    private void CopyStacks(ContainerId container, List<Stack> into)
    {
        if (!_inventory.TryGetContainer(container, out var grid))
            return;
        foreach (var entry in grid.Entries)
            into.Add(entry.Stack);
    }

    private bool TryDeposit(ContainerId to, Stack stack)
        => _inventory.Apply(Actor.System, new Deposit(to, stack)) is Accepted;

    private readonly record struct BoundPlayer(
        PlayerBody Body,
        ContainerId Hotbar,
        ContainerId Inventory,
        ContainerId? Backpack,
        ContainerId? Cursor);
}
