using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Content;

public static class ContentRefs
{
    public static void Validate(
        ItemDef[] items,
        ContainerDef[] containers,
        MailKindDef[] kinds,
        MailMixDef mix,
        DestinationTypeDef[] dests,
        BuildingDef[] buildings,
        RecipeDef[] recipes,
        ShopItemDef[] shop)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        if (containers is null) throw new ArgumentNullException(nameof(containers));
        if (kinds is null) throw new ArgumentNullException(nameof(kinds));
        if (mix is null) throw new ArgumentNullException(nameof(mix));
        if (dests is null) throw new ArgumentNullException(nameof(dests));
        if (buildings is null) throw new ArgumentNullException(nameof(buildings));
        if (recipes is null) throw new ArgumentNullException(nameof(recipes));
        if (shop is null) throw new ArgumentNullException(nameof(shop));

        var itemIds = Index(items, d => d.Id);
        var containerIds = Index(containers, d => d.Id);
        var kindIds = Index(kinds, d => d.Id);
        var destIds = Index(dests, d => d.Id);
        var buildingById = Map(buildings, d => d.Id);
        var recipeById = Map(recipes, d => d.Id);
        var shopById = Map(shop, d => d.Id);

        for (int i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            for (int a = 0; a < kind.AcceptedBy.Length; a++)
            {
                string dest = kind.AcceptedBy[a];
                if (!destIds.Contains(dest))
                {
                    throw new InvalidOperationException(
                        $"mail/kinds.json: '{kind.Id}' acceptedBy unknown destination '{dest}'.");
                }
            }
        }

        for (int i = 0; i < mix.Shifts.Length; i++)
        {
            var shift = mix.Shifts[i];
            foreach (string key in shift.Shares.Keys)
            {
                if (!kindIds.Contains(key))
                {
                    throw new InvalidOperationException(
                        $"mail/mix.json: shift {shift.Shift} shares unknown mail kind '{key}'.");
                }
            }
        }

        for (int i = 0; i < recipes.Length; i++)
        {
            var recipe = recipes[i];
            if (!buildingById.ContainsKey(recipe.ProducesBuilding))
            {
                throw new InvalidOperationException(
                    $"recipes: '{recipe.Id}' produces unknown building '{recipe.ProducesBuilding}'.");
            }

            for (int n = 0; n < recipe.Inputs.Length; n++)
            {
                string item = recipe.Inputs[n].Item;
                if (!itemIds.Contains(item))
                    throw new InvalidOperationException($"recipes: '{recipe.Id}' input unknown item '{item}'.");
            }

            if (recipe.Blueprint is string blueprint)
            {
                if (!shopById.TryGetValue(blueprint, out var row) || row.Kind != ShopKind.Blueprint)
                {
                    throw new InvalidOperationException(
                        $"recipes: '{recipe.Id}' blueprint '{blueprint}' is not a shop item of kind blueprint.");
                }
            }
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            var building = buildings[i];
            if (!recipeById.TryGetValue(building.Recipe, out var recipe))
                throw new InvalidOperationException($"buildings: '{building.Id}' recipe unknown '{building.Recipe}'.");
            if (recipe.ProducesBuilding != building.Id)
            {
                throw new InvalidOperationException(
                    $"buildings: '{building.Id}' recipe '{building.Recipe}' produces '{recipe.ProducesBuilding}'.");
            }

            if (building.Container is string container && !containerIds.Contains(container))
                throw new InvalidOperationException($"buildings: '{building.Id}' container unknown '{container}'.");
        }

        for (int i = 0; i < shop.Length; i++)
        {
            var row = shop[i];
            if (row.GrantItem is string item && !itemIds.Contains(item))
                throw new InvalidOperationException($"shop: '{row.Id}' grants unknown item '{item}'.");
        }

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item.Weapon?.AmmoItem is string ammo && !itemIds.Contains(ammo))
                throw new InvalidOperationException($"items: '{item.Id}' weapon ammoItem unknown '{ammo}'.");
        }
    }

    private static HashSet<string> Index<T>(T[] defs, Func<T, string> id)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < defs.Length; i++)
            set.Add(id(defs[i]));
        return set;
    }

    private static Dictionary<string, T> Map<T>(T[] defs, Func<T, string> id)
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        for (int i = 0; i < defs.Length; i++)
            map[id(defs[i])] = defs[i];
        return map;
    }
}
