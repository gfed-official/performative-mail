using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public sealed class BuildModeState
{
    private readonly BuildingDef[] _all;
    private readonly BuildCategory[] _categories;

    public BuildModeState(IReadOnlyList<BuildingDef> buildings)
    {
        if (buildings is null) throw new ArgumentNullException(nameof(buildings));
        _all = new BuildingDef[buildings.Count];
        for (int i = 0; i < buildings.Count; i++)
            _all[i] = buildings[i] ?? throw new ArgumentNullException(nameof(buildings));
        Array.Sort(_all, (a, b) => string.CompareOrdinal(a.Id, b.Id));
        _categories = UsedCategories(_all);
        Category = _categories.Length == 0 ? BuildCategory.Transport : _categories[0];
        SelectedId = FirstIn(Category);
    }

    public bool IsOpen { get; private set; }

    public BuildCategory Category { get; private set; }

    public string SelectedId { get; private set; }

    public Facing Facing { get; private set; }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void SetCategory(BuildCategory category)
    {
        if (!HasCategory(category))
            return;
        Category = category;
        if (!InCategory(SelectedId, category))
            SelectedId = FirstIn(category);
    }

    public void SelectCategoryIndex(int index)
    {
        if ((uint)index >= (uint)_categories.Length)
            return;
        SetCategory(_categories[index]);
    }

    public bool Select(string id)
    {
        if (!TryFind(id, out var def))
            return false;
        Category = BuildCategories.Of(def.Behaviour);
        SelectedId = def.Id;
        return true;
    }

    public void Cycle(int delta)
    {
        var choices = Choices(Category);
        if (choices.Count == 0)
            return;
        int at = 0;
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i].Id != SelectedId)
                continue;
            at = i;
            break;
        }

        int next = at + delta;
        int n = choices.Count;
        next %= n;
        if (next < 0)
            next += n;
        SelectedId = choices[next].Id;
    }

    public void Rotate() =>
        Facing = (Facing)(((int)Facing + 1) % 4);

    public bool TryPipette(string id) => Select(id);

    public BuildFrame Frame(bool valid, string reason)
    {
        var tabs = new BuildCategoryTab[_categories.Length];
        for (int i = 0; i < _categories.Length; i++)
        {
            var category = _categories[i];
            tabs[i] = new BuildCategoryTab(category, BuildCategories.Label(category), category == Category);
        }

        string name = "";
        if (TryFind(SelectedId, out var selected))
            name = selected.Name;

        return new BuildFrame(
            IsOpen,
            Category,
            SelectedId,
            name,
            Facing,
            valid,
            reason ?? "",
            tabs,
            Choices(Category));
    }

    private IReadOnlyList<BuildChoice> Choices(BuildCategory category)
    {
        var rows = new List<BuildChoice>();
        for (int i = 0; i < _all.Length; i++)
        {
            var def = _all[i];
            if (BuildCategories.Of(def.Behaviour) != category)
                continue;
            rows.Add(new BuildChoice(def.Id, def.Name, def.Id == SelectedId));
        }

        return rows;
    }

    private string FirstIn(BuildCategory category)
    {
        for (int i = 0; i < _all.Length; i++)
        {
            if (BuildCategories.Of(_all[i].Behaviour) == category)
                return _all[i].Id;
        }

        return "";
    }

    private bool InCategory(string id, BuildCategory category) =>
        TryFind(id, out var def) && BuildCategories.Of(def.Behaviour) == category;

    private bool TryFind(string id, out BuildingDef def)
    {
        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i].Id != id)
                continue;
            def = _all[i];
            return true;
        }

        def = null!;
        return false;
    }

    private bool HasCategory(BuildCategory category)
    {
        for (int i = 0; i < _categories.Length; i++)
        {
            if (_categories[i] == category)
                return true;
        }

        return false;
    }

    private static BuildCategory[] UsedCategories(BuildingDef[] buildings)
    {
        var seen = new bool[6];
        for (int i = 0; i < buildings.Length; i++)
            seen[(int)BuildCategories.Of(buildings[i].Behaviour)] = true;

        int n = 0;
        for (int i = 0; i < seen.Length; i++)
        {
            if (seen[i])
                n++;
        }

        var rows = new BuildCategory[n];
        int w = 0;
        for (int i = 0; i < seen.Length; i++)
        {
            if (!seen[i])
                continue;
            rows[w++] = (BuildCategory)i;
        }

        return rows;
    }
}
