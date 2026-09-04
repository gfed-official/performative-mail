using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Run;

public readonly record struct HeldSeat(uint Account, EntityId Player, uint DropTick);

public sealed class DisconnectGrace
{
    public static readonly int HoldTicks = TickClock.TicksFromSeconds(120);

    public static readonly int EmptyTicks = TickClock.TicksFromSeconds(60);

    private readonly Dictionary<uint, HeldSeat> _held = new();
    private readonly Dictionary<uint, uint> _byAccount = new();
    private uint? _emptyAt;

    public uint Now { get; private set; }

    public bool EndedWithoutResults { get; private set; }

    public int HeldCount => _held.Count;

    public bool IsHeld(EntityId player) => _held.ContainsKey(player.Value);

    public bool Hold(uint account, EntityId player, uint now, int connectedAfter)
    {
        if (_held.ContainsKey(player.Value))
            return false;

        if (now > Now)
            Now = now;

        _held[player.Value] = new HeldSeat(account, player, now + (uint)HoldTicks);
        if (account != 0)
            _byAccount[account] = player.Value;

        if (connectedAfter == 0)
            _emptyAt ??= now + (uint)EmptyTicks;

        return true;
    }

    public bool TryResume(uint account, out EntityId player)
    {
        player = default;
        if (account == 0)
            return false;
        if (!_byAccount.TryGetValue(account, out var id))
            return false;
        if (!_held.TryGetValue(id, out var held))
            return false;

        _byAccount.Remove(account);
        _held.Remove(id);
        _emptyAt = null;
        player = held.Player;
        return true;
    }

    public void AdvanceTo(uint tick)
    {
        if (tick < Now)
            throw new ArgumentOutOfRangeException(nameof(tick), tick, null);

        Now = tick;
        if (!EndedWithoutResults && _emptyAt is uint empty && Now >= empty)
            EndedWithoutResults = true;
    }

    public List<EntityId> TakeExpired()
    {
        var dropped = new List<EntityId>();
        foreach (var pair in _held)
        {
            if (Now < pair.Value.DropTick)
                continue;
            dropped.Add(pair.Value.Player);
        }

        for (int i = 0; i < dropped.Count; i++)
        {
            var player = dropped[i];
            var held = _held[player.Value];
            _held.Remove(player.Value);
            if (held.Account != 0)
                _byAccount.Remove(held.Account);
        }

        return dropped;
    }
}
