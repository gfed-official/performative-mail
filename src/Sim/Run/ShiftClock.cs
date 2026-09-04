using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Run;

public sealed class ShiftClock
{
    private readonly BalanceTable _balance;
    private readonly HashSet<uint> _connected = new HashSet<uint>();
    private readonly HashSet<uint> _ready = new HashSet<uint>();

    public ShiftClock(BalanceTable balance, RunState state, uint now = 0)
    {
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        State = state;
        Now = now;
    }

    public RunState State { get; private set; }

    public uint Now { get; private set; }

    public bool Paused { get; private set; }

    public int ConnectedCount => _connected.Count;

    public bool Solo => _connected.Count == 1;

    public void Connect(uint playerId)
    {
        _connected.Add(playerId);
        if (!Solo)
            Paused = false;
    }

    public bool Disconnect(uint playerId)
    {
        bool removed = _connected.Remove(playerId);
        _ready.Remove(playerId);
        if (!Solo)
            Paused = false;
        if (removed)
            TryReadyExit();
        return removed;
    }

    public bool SetReady(uint playerId, bool ready)
    {
        if (!_connected.Contains(playerId))
            return false;

        if (ready)
            _ready.Add(playerId);
        else
            _ready.Remove(playerId);

        return TryReadyExit();
    }

    public bool TrySetPaused(bool paused)
    {
        if (paused && !Solo)
            return false;

        Paused = paused;
        return true;
    }

    public bool TryEnter(RunPhase to)
    {
        if (!RunTransitions.IsLegal(State.Phase, to, State.Shift))
            return false;

        byte nextShift = RunState.NextShift(State.Phase, to, State.Shift);
        uint deadline = to == RunPhase.Raid
            ? State.PhaseDeadlineTick
            : DeadlineAt(to, nextShift);

        if (!State.TryTransition(to, deadline, out var next))
            return false;

        State = next;
        _ready.Clear();
        return true;
    }

    public bool TryAllPicked()
    {
        if (State.Phase != RunPhase.Draft)
            return false;

        return TryEnter(State.Shift < RunState.ShiftCount ? RunPhase.Prep : RunPhase.Victory);
    }

    public void AdvanceTo(uint tick)
    {
        if (tick < Now)
            throw new ArgumentOutOfRangeException(nameof(tick), tick, null);

        if (!Paused)
        {
            while (Now < tick)
            {
                Now++;
                StepPhase();
            }
        }

        TryReadyExit();
    }

    public static bool ShouldReplicate(uint now, RunPhase phase, bool paused, uint lastNow, RunPhase lastPhase, bool lastPaused)
    {
        if (phase != lastPhase || paused != lastPaused)
            return true;

        return now / (uint)TickClock.TickHz != lastNow / (uint)TickClock.TickHz;
    }

    private uint DeadlineAt(RunPhase phase, byte shift)
    {
        int seconds = ShiftDurations.Seconds(phase, shift, _balance);
        if (seconds <= 0)
            return 0;

        return Now + (uint)TickClock.TicksFromSeconds(seconds);
    }

    private void StepPhase()
    {
        if (State.Phase == RunPhase.Delivery
            && State.Shift >= 2
            && State.RemainingTicks(Now) <= (uint)TickClock.TicksFromSeconds(ShiftDurations.RaidWindowSeconds))
        {
            TryEnter(RunPhase.Raid);
        }

        if (State.PhaseDeadlineTick == 0 || Now < State.PhaseDeadlineTick)
            return;

        RunPhase? next = ExpiryTarget(State);
        if (next is RunPhase to)
            TryEnter(to);
    }

    private bool TryReadyExit()
    {
        if (State.Phase != RunPhase.Prep || _connected.Count == 0 || _ready.Count != _connected.Count)
            return false;

        return TryEnter(RunPhase.Delivery);
    }

    private static RunPhase? ExpiryTarget(in RunState state) => state.Phase switch
    {
        RunPhase.Prep => RunPhase.Delivery,
        RunPhase.Delivery => state.Shift == 1 ? RunPhase.Payday : null,
        RunPhase.Raid => RunPhase.Payday,
        RunPhase.Draft => state.Shift < RunState.ShiftCount ? RunPhase.Prep : RunPhase.Victory,
        _ => null,
    };
}
