using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Building;

public readonly record struct ConstructRecord(
    EntityId Id,
    string DefId,
    TileCoord Tile,
    Facing Rotation,
    EntityId Owner,
    int Hp,
    int MaxHp);

public enum PlaceReject : byte
{
    UnknownBuilding,
    UnknownRecipe,
    OutOfBounds,
    Water,
    Street,
    Slope,
    Occupied,
    UnknownItem,
    MissingInput
}

public abstract record PlaceResult;

public sealed record Placed(ConstructRecord Construct) : PlaceResult;

public sealed record PlaceRejected(PlaceReject Reason) : PlaceResult;

public enum PlaceLineReject : byte
{
    UnknownBuilding,
    NotDragLine,
    NotStraight
}

public readonly record struct LineTile(TileCoord Tile, PlaceResult Result);

public abstract record PlaceLineResult;

public sealed record PlaceLineApplied(IReadOnlyList<LineTile> Tiles) : PlaceLineResult;

public sealed record PlaceLineRejected(PlaceLineReject Reason) : PlaceLineResult;

public enum DeconstructReject : byte
{
    UnknownConstruct,
    UnknownBuilding,
    UnknownRecipe,
    UnknownItem,
    MissingInventory,
    NoRoom
}

public abstract record DeconstructResult;

public sealed record Deconstructed(ConstructRecord Construct) : DeconstructResult;

public sealed record DeconstructRejected(DeconstructReject Reason) : DeconstructResult;

public sealed class ConstructRegistry
{
    private readonly Dictionary<string, BuildingDef> _buildings;
    private readonly Dictionary<string, RecipeDef> _recipes;
    private readonly PlacementField _field;
    private readonly InventorySystem? _inventory;
    private readonly ContainerId _from;
    private readonly Dictionary<string, ItemDefId> _itemIds;
    private readonly Dictionary<uint, ConstructRecord> _byId = new Dictionary<uint, ConstructRecord>();
    private readonly Dictionary<TileCoord, EntityId> _at = new Dictionary<TileCoord, EntityId>();
    private readonly List<ConstructRecord> _order = new List<ConstructRecord>();
    private readonly List<FlattenedTile> _flattened = new List<FlattenedTile>();
    private uint _nextCounter = 1;

    public ConstructRegistry(
        IReadOnlyList<BuildingDef> buildings,
        IReadOnlyList<RecipeDef> recipes,
        PlacementField field,
        InventorySystem? inventory = null,
        ContainerId from = default,
        IReadOnlyDictionary<string, ItemDefId>? itemIds = null)
    {
        if (buildings is null) throw new ArgumentNullException(nameof(buildings));
        if (recipes is null) throw new ArgumentNullException(nameof(recipes));
        _field = field ?? throw new ArgumentNullException(nameof(field));
        _inventory = inventory;
        _from = from;
        _buildings = new Dictionary<string, BuildingDef>(buildings.Count, StringComparer.Ordinal);
        for (int i = 0; i < buildings.Count; i++)
        {
            var def = buildings[i] ?? throw new ArgumentNullException(nameof(buildings));
            _buildings.Add(def.Id, def);
        }

        _recipes = new Dictionary<string, RecipeDef>(recipes.Count, StringComparer.Ordinal);
        for (int i = 0; i < recipes.Count; i++)
        {
            var def = recipes[i] ?? throw new ArgumentNullException(nameof(recipes));
            _recipes.Add(def.Id, def);
        }

        _itemIds = new Dictionary<string, ItemDefId>(StringComparer.Ordinal);
        if (itemIds is null) return;
        foreach (var pair in itemIds)
            _itemIds[pair.Key] = pair.Value;
    }

    public int Count => _order.Count;

    public IReadOnlyList<ConstructRecord> All => _order;

    public IReadOnlyList<FlattenedTile> FlattenedTiles => _flattened;

    public bool TryGet(EntityId id, out ConstructRecord record) =>
        _byId.TryGetValue(id.Value, out record);

    public PlaceResult TryPlace(string buildingId, TileCoord tile, Facing rotation, EntityId owner = default)
    {
        if (!_buildings.TryGetValue(buildingId, out var building))
            return new PlaceRejected(PlaceReject.UnknownBuilding);
        if (!_recipes.TryGetValue(building.Recipe, out var recipe))
            return new PlaceRejected(PlaceReject.UnknownRecipe);

        var covered = Covered(building, tile, rotation);
        for (int i = 0; i < covered.Length; i++)
        {
            var at = covered[i];
            if (!_field.InBounds(at))
                return new PlaceRejected(PlaceReject.OutOfBounds);
            if (building.OnWater == WaterPlacement.None && _field.IsWater(at))
                return new PlaceRejected(PlaceReject.Water);
            if (!building.OnStreet && _field.IsStreet(at))
                return new PlaceRejected(PlaceReject.Street);
            if (_at.ContainsKey(at))
                return new PlaceRejected(PlaceReject.Occupied);
        }

        if (!_field.TryPlanFlatten(covered, out var planned))
            return new PlaceRejected(PlaceReject.Slope);

        if (!TryConsume(recipe, out var reject))
            return new PlaceRejected(reject);

        _field.ApplyFlatten(planned);
        for (int i = 0; i < planned.Length; i++)
            _flattened.Add(planned[i]);

        var id = EntityId.FromClassAndCounter(EntityClass.Construct, _nextCounter++);
        var row = new ConstructRecord(id, building.Id, tile, rotation, owner, building.Hp, building.Hp);
        _byId.Add(id.Value, row);
        for (int i = 0; i < covered.Length; i++)
            _at.Add(covered[i], id);
        _order.Add(row);
        return new Placed(row);
    }

    public PlaceLineResult TryPlaceLine(
        string buildingId,
        TileCoord from,
        TileCoord to,
        Facing rotation,
        EntityId owner = default)
    {
        if (!_buildings.TryGetValue(buildingId, out var building))
            return new PlaceLineRejected(PlaceLineReject.UnknownBuilding);
        if (!building.DragLine)
            return new PlaceLineRejected(PlaceLineReject.NotDragLine);
        if (!TryLineTiles(from, to, out var tiles))
            return new PlaceLineRejected(PlaceLineReject.NotStraight);

        var results = new LineTile[tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
            results[i] = new LineTile(tiles[i], TryPlace(buildingId, tiles[i], rotation, owner));
        return new PlaceLineApplied(results);
    }

    public DeconstructResult TryDeconstruct(EntityId id, double salvageRatio)
    {
        if (double.IsNaN(salvageRatio) || salvageRatio < 0 || salvageRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(salvageRatio));
        if (!_byId.TryGetValue(id.Value, out var row))
            return new DeconstructRejected(DeconstructReject.UnknownConstruct);
        if (!_buildings.TryGetValue(row.DefId, out var building))
            return new DeconstructRejected(DeconstructReject.UnknownBuilding);
        if (!_recipes.TryGetValue(building.Recipe, out var recipe))
            return new DeconstructRejected(DeconstructReject.UnknownRecipe);
        if (_inventory is null || !_inventory.TryGetContainer(_from, out _))
            return new DeconstructRejected(DeconstructReject.MissingInventory);

        var refunds = new List<ItemStack>();
        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            var input = recipe.Inputs[i];
            int give = RefundCount(input.Count, salvageRatio);
            if (give < 1) continue;
            if (!_itemIds.TryGetValue(input.Item, out var itemId))
                return new DeconstructRejected(DeconstructReject.UnknownItem);
            refunds.Add(new ItemStack(itemId, give));
        }

        int accepted = 0;
        for (int i = 0; i < refunds.Count; i++)
        {
            if (_inventory.Apply(Actor.System, new Deposit(_from, refunds[i])) is Accepted)
            {
                accepted++;
                continue;
            }

            for (int j = 0; j < accepted; j++)
                TakeBack(refunds[j]);
            return new DeconstructRejected(DeconstructReject.NoRoom);
        }

        Unregister(row, building);
        return new Deconstructed(row);
    }

    private void TakeBack(ItemStack stack)
    {
        if (_inventory is null || !_inventory.TryGetContainer(_from, out var grid))
            return;

        int need = stack.Count;
        var takes = new List<(EntryId Id, int Count)>();
        foreach (var entry in grid.Entries)
        {
            if (need < 1) break;
            if (entry.Stack is not ItemStack item || !item.Item.Equals(stack.Item))
                continue;
            int take = item.Count < need ? item.Count : need;
            takes.Add((entry.Id, take));
            need -= take;
        }

        for (int i = 0; i < takes.Count; i++)
        {
            var step = takes[i];
            _inventory.Apply(Actor.System, new Withdraw(_from, step.Id, Amount.Of(step.Count)));
        }
    }

    private void Unregister(ConstructRecord row, BuildingDef building)
    {
        var covered = Covered(building, row.Tile, row.Rotation);
        _byId.Remove(row.Id.Value);
        for (int i = 0; i < covered.Length; i++)
            _at.Remove(covered[i]);
        _order.Remove(row);
    }

    private static int RefundCount(int count, double salvageRatio)
    {
        if (salvageRatio >= 1) return count;
        if (salvageRatio <= 0) return 0;
        return (int)(count * salvageRatio);
    }

    private static bool TryLineTiles(TileCoord from, TileCoord to, out TileCoord[] tiles)
    {
        if (from.X != to.X && from.Y != to.Y)
        {
            tiles = Array.Empty<TileCoord>();
            return false;
        }

        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        int n = 1 + Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
        tiles = new TileCoord[n];
        int x = from.X;
        int y = from.Y;
        for (int i = 0; i < n; i++)
        {
            tiles[i] = new TileCoord(x, y);
            x += dx;
            y += dy;
        }

        return true;
    }

    private bool TryConsume(RecipeDef recipe, out PlaceReject reject)
    {
        if (_inventory is null)
        {
            reject = PlaceReject.MissingInput;
            return false;
        }

        if (!_inventory.TryGetContainer(_from, out var grid))
        {
            reject = PlaceReject.MissingInput;
            return false;
        }

        var takes = new List<(EntryId Id, int Count)>();
        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            var input = recipe.Inputs[i];
            if (!_itemIds.TryGetValue(input.Item, out var itemId))
            {
                reject = PlaceReject.UnknownItem;
                return false;
            }

            int need = input.Count;
            foreach (var entry in grid.Entries)
            {
                if (need < 1) break;
                if (entry.Stack is not ItemStack item || !item.Item.Equals(itemId))
                    continue;
                int take = item.Count < need ? item.Count : need;
                takes.Add((entry.Id, take));
                need -= take;
            }

            if (need > 0)
            {
                reject = PlaceReject.MissingInput;
                return false;
            }
        }

        for (int i = 0; i < takes.Count; i++)
        {
            var step = takes[i];
            var result = _inventory.Apply(Actor.System, new Withdraw(_from, step.Id, Amount.Of(step.Count)));
            if (result is not Accepted)
            {
                reject = PlaceReject.MissingInput;
                return false;
            }
        }

        reject = default;
        return true;
    }

    private static TileCoord[] Covered(BuildingDef building, TileCoord origin, Facing rotation)
    {
        int w = building.Footprint.W;
        int h = building.Footprint.H;
        if (w == 1 || h == 1)
        {
            int length = w >= h ? w : h;
            Step(rotation, out int sx, out int sy);
            var walk = new TileCoord[length];
            for (int i = 0; i < length; i++)
                walk[i] = new TileCoord(origin.X + sx * i, origin.Y + sy * i);
            return walk;
        }

        if ((rotation == Facing.East || rotation == Facing.West) && !building.Footprint.IsSquare)
        {
            int swap = w;
            w = h;
            h = swap;
        }

        var tiles = new TileCoord[w * h];
        int n = 0;
        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
                tiles[n++] = new TileCoord(origin.X + dx, origin.Y + dy);
        }

        return tiles;
    }

    private static void Step(Facing facing, out int dx, out int dy)
    {
        switch (facing)
        {
            case Facing.North:
                dx = 0;
                dy = 1;
                return;
            case Facing.East:
                dx = 1;
                dy = 0;
                return;
            case Facing.South:
                dx = 0;
                dy = -1;
                return;
            case Facing.West:
                dx = -1;
                dy = 0;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
        }
    }
}
