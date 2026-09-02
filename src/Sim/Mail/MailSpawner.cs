using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;
using InventoryRejected = PerformativeMail.Sim.Inventory.Rejected;

namespace PerformativeMail.Sim.Mail;

public sealed class MailSpawner
{
    private enum Shift1Kind : byte { Letter, Small, Medium }

    private static readonly bool[] Makeable = BuildMakeable(MailSpawnConstants.Shift1SpawnValueCents);

    private readonly WorldAtlas _atlas;
    private readonly MailRegistry _registry;
    private readonly InventorySystem _inventory;
    private readonly ContainerId _intake;
    private readonly Random _spawnRng;
    private readonly Random _addressRng;
    private readonly int _jitterSeconds;
    private readonly Queue<MailItem> _backlog = new();
    private uint _nextBatchTick;
    private int _spawnedValue;
    private int _batchesEmitted;

    public MailSpawner(
        WorldAtlas atlas,
        MailRegistry registry,
        InventorySystem inventory,
        ContainerId intake,
        int seed,
        int jitterSeconds = MailSpawnConstants.BatchJitterSeconds)
    {
        _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        if (jitterSeconds < 0) throw new ArgumentOutOfRangeException(nameof(jitterSeconds));
        if (_atlas.DeliverableAddresses.Count == 0)
            throw new ArgumentException("Atlas has no deliverable addresses.", nameof(atlas));

        _intake = intake;
        _jitterSeconds = jitterSeconds;
        _spawnRng = new Random(seed);
        _addressRng = new Random(seed);
        _nextBatchTick = NextTickAfter(0);
    }

    public int SpawnedValue => _spawnedValue;

    public IReadOnlyList<MailItem> Backlog
    {
        get
        {
            var copy = new MailItem[_backlog.Count];
            _backlog.CopyTo(copy, 0);
            return copy;
        }
    }

    public void Step(uint tick)
    {
        FlushBacklog();
        while (_spawnedValue < MailSpawnConstants.Shift1SpawnValueCents && tick >= _nextBatchTick)
        {
            EmitBatch();
            _nextBatchTick = NextTickAfter(_nextBatchTick);
        }

        if (tick >= MailSpawnConstants.Shift1DeliveryTicks &&
            _spawnedValue < MailSpawnConstants.Shift1SpawnValueCents)
        {
            EmitBatch();
        }
    }

    private void EmitBatch()
    {
        int remainingCap = MailSpawnConstants.Shift1SpawnValueCents - _spawnedValue;
        if (remainingCap < MailKinds.LetterBaseValue)
        {
            _batchesEmitted++;
            return;
        }

        int remainingBatches = MailSpawnConstants.BatchesPerShift - _batchesEmitted;
        if (remainingBatches < 1) remainingBatches = 1;
        int batchTarget = remainingCap / remainingBatches;
        if (batchTarget < MailKinds.LetterBaseValue)
            batchTarget = remainingCap;

        var kinds = new List<MailKindId>();
        int batchValue = 0;
        while (true)
        {
            int capLeft = MailSpawnConstants.Shift1SpawnValueCents - _spawnedValue;
            int batchLeft = batchTarget - batchValue;
            if (!TryPickKind(capLeft, batchLeft, allowExceed: kinds.Count == 0, out var kind, out int value))
                break;

            kinds.Add(kind);
            batchValue += value;
            _spawnedValue += value;
            if (batchValue >= batchTarget) break;
        }

        if (kinds.Count > 0)
        {
            var addresses = StreetStreakPicker.Pick(
                _atlas.DeliverableAddresses,
                kinds.Count,
                MailSpawnConstants.StreetStreakRatio,
                _addressRng);

            for (int i = 0; i < kinds.Count; i++)
            {
                var kind = kinds[i];
                var item = new MailItem(
                    _registry.Allocate(),
                    kind,
                    addresses[i],
                    MailKinds.ValueAtSpawn(kind, _atlas.DistrictId, MailSpawnConstants.Shift1),
                    MailSpawnConstants.Shift1,
                    MailSpawnConstants.Shift1);
                if (!_registry.Register(item))
                    throw new InvalidOperationException("Allocated mail id was already registered.");
                TryDepositOrBacklog(item);
            }
        }

        _batchesEmitted++;
    }

    private bool TryPickKind(int remainingCap, int batchLeft, bool allowExceed, out MailKindId kind, out int value)
    {
        kind = default;
        value = 0;
        var rolled = RollShift1Kind();
        var order = FallbackOrder(rolled);
        for (int i = 0; i < order.Length; i++)
        {
            var candidate = IdOf(order[i]);
            int cost = MailKinds.BaseValue(candidate);
            if (cost > remainingCap) continue;
            if (cost > batchLeft && !allowExceed) continue;
            if (!Makeable[remainingCap - cost]) continue;
            kind = candidate;
            value = cost;
            return true;
        }

        return false;
    }

    private Shift1Kind RollShift1Kind()
    {
        double roll = _spawnRng.NextDouble();
        if (roll < MailSpawnConstants.Shift1LetterShare)
            return Shift1Kind.Letter;
        if (roll < MailSpawnConstants.Shift1LetterShare + MailSpawnConstants.Shift1SmallShare)
            return Shift1Kind.Small;
        return Shift1Kind.Medium;
    }

    private static Shift1Kind[] FallbackOrder(Shift1Kind rolled)
    {
        switch (rolled)
        {
            case Shift1Kind.Letter:
                return new[] { Shift1Kind.Letter, Shift1Kind.Small, Shift1Kind.Medium };
            case Shift1Kind.Small:
                return new[] { Shift1Kind.Small, Shift1Kind.Letter, Shift1Kind.Medium };
            case Shift1Kind.Medium:
                return new[] { Shift1Kind.Medium, Shift1Kind.Small, Shift1Kind.Letter };
            default:
                throw Unexpected(rolled);
        }
    }

    private static MailKindId IdOf(Shift1Kind kind)
    {
        switch (kind)
        {
            case Shift1Kind.Letter: return MailKinds.Letter;
            case Shift1Kind.Small: return MailKinds.SmallPackage;
            case Shift1Kind.Medium: return MailKinds.MediumPackage;
            default:
                throw Unexpected(kind);
        }
    }

    private void TryDepositOrBacklog(MailItem item)
    {
        var stack = MailStack.Single(item.Kind, item.Address, item.Id);
        if (_inventory.Apply(Actor.System, new Deposit(_intake, stack)) is InventoryRejected)
            _backlog.Enqueue(item);
    }

    private void FlushBacklog()
    {
        while (_backlog.Count > 0)
        {
            var item = _backlog.Peek();
            var stack = MailStack.Single(item.Kind, item.Address, item.Id);
            if (_inventory.Apply(Actor.System, new Deposit(_intake, stack)) is InventoryRejected)
                return;
            _backlog.Dequeue();
        }
    }

    private uint NextTickAfter(uint from)
    {
        int next = (int)from + MailSpawnConstants.BatchIntervalTicks + JitterTicks();
        return next < 1 ? 1 : (uint)next;
    }

    private int JitterTicks()
    {
        if (_jitterSeconds == 0) return 0;
        int span = _jitterSeconds * TickClock.TickHz;
        return _spawnRng.Next(-span, span + 1);
    }

    private static bool[] BuildMakeable(int cap)
    {
        var ok = new bool[cap + 1];
        ok[0] = true;
        var coins = new[]
        {
            (int)MailKinds.LetterBaseValue,
            (int)MailKinds.SmallPackageBaseValue,
            (int)MailKinds.MediumPackageBaseValue
        };
        for (int i = 0; i <= cap; i++)
        {
            if (!ok[i]) continue;
            for (int c = 0; c < coins.Length; c++)
            {
                int n = i + coins[c];
                if (n <= cap) ok[n] = true;
            }
        }

        return ok;
    }

    private static Exception Unexpected(Shift1Kind kind)
        => new ArgumentOutOfRangeException(nameof(kind), kind, null);
}
