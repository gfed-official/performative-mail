using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public sealed class OilPump
{
    public const string BuildingId = "pump";
    public const string OutputItemId = "oil_can";
    public const int OutputCount = 1;

    public static int OutputPeriodTicks => TickClock.TicksFromSeconds(45);

    private int _cooldown = OutputPeriodTicks;

    public OilPump(TileCoord tile)
    {
        Tile = tile;
    }

    public TileCoord Tile { get; }

    public bool TryReady()
    {
        if (_cooldown > 0)
        {
            _cooldown--;
            if (_cooldown > 0) return false;
        }

        return true;
    }

    public void Complete()
    {
        _cooldown = OutputPeriodTicks;
    }
}

public sealed class OilPumpNetwork
{
    private readonly List<OilPump> _pumps = new List<OilPump>();
    private readonly Dictionary<TileCoord, ContainerId> _outputs = new Dictionary<TileCoord, ContainerId>();
    private InventorySystem? _inventory;
    private ItemDefId _oilCan;

    public IReadOnlyList<OilPump> Pumps => _pumps;

    public void BindInventory(InventorySystem inventory, IReadOnlyDictionary<string, ItemDefId> itemIds)
    {
        if (inventory is null) throw new ArgumentNullException(nameof(inventory));
        if (itemIds is null) throw new ArgumentNullException(nameof(itemIds));
        if (!itemIds.TryGetValue(OilPump.OutputItemId, out var oilCan))
            throw new InvalidOperationException($"Missing harvest item '{OilPump.OutputItemId}'.");

        _inventory = inventory;
        _oilCan = oilCan;
    }

    public void BindOutput(TileCoord tile, ContainerId output) => _outputs[tile] = output;

    public void Compile(IReadOnlyList<ConstructRecord> constructs)
    {
        if (constructs is null) throw new ArgumentNullException(nameof(constructs));
        _pumps.Clear();

        var rows = new List<ConstructRecord>();
        for (int i = 0; i < constructs.Count; i++)
        {
            var row = constructs[i];
            if (string.Equals(row.DefId, OilPump.BuildingId, StringComparison.Ordinal))
                rows.Add(row);
        }

        rows.Sort((a, b) =>
        {
            int byX = a.Tile.X.CompareTo(b.Tile.X);
            return byX != 0 ? byX : a.Tile.Y.CompareTo(b.Tile.Y);
        });

        for (int i = 0; i < rows.Count; i++)
            _pumps.Add(new OilPump(rows[i].Tile));
    }

    public void Step()
    {
        for (int i = 0; i < _pumps.Count; i++)
        {
            var pump = _pumps[i];
            if (!pump.TryReady()) continue;
            if (!TryDeposit(pump.Tile)) continue;
            pump.Complete();
        }
    }

    public void StepTicks(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, null);
        for (int n = 0; n < ticks; n++)
            Step();
    }

    private bool TryDeposit(TileCoord tile)
    {
        if (_inventory is null) return false;
        if (!_outputs.TryGetValue(tile, out var output)) return false;
        return _inventory.Apply(
            Actor.System,
            new Deposit(output, new ItemStack(_oilCan, OilPump.OutputCount))) is Accepted;
    }
}
