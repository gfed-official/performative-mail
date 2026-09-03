using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Content;

public enum WaterPlacement
{
    None,
    Shallow,
    Deep,
    Shore
}

public enum BuildingBehaviour
{
    Belt,
    Pipe,
    Splitter,
    Merger,
    Sorter,
    Inserter,
    Container,
    Wall,
    Gate,
    Spike,
    Turret,
    Alarm,
    VehicleDepot,
    Port,
    Pump,
    Pier
}

public sealed class BuildingDef
{
    public BuildingDef(
        string id,
        string name,
        Footprint footprint,
        int rotations,
        int hp,
        bool onStreet,
        WaterPlacement onWater,
        double maxSlopeDeg,
        bool dragLine,
        BuildingBehaviour behaviour,
        string? container,
        string recipe,
        double ruinRebuildRatio,
        string[] tags)
    {
        Id = id;
        Name = name;
        Footprint = footprint;
        Rotations = rotations;
        Hp = hp;
        OnStreet = onStreet;
        OnWater = onWater;
        MaxSlopeDeg = maxSlopeDeg;
        DragLine = dragLine;
        Behaviour = behaviour;
        Container = container;
        Recipe = recipe;
        RuinRebuildRatio = ruinRebuildRatio;
        Tags = tags;
    }

    public string Id { get; }

    public string Name { get; }

    public Footprint Footprint { get; }

    public int Rotations { get; }

    public int Hp { get; }

    public bool OnStreet { get; }

    public WaterPlacement OnWater { get; }

    public double MaxSlopeDeg { get; }

    public bool DragLine { get; }

    public BuildingBehaviour Behaviour { get; }

    public string? Container { get; }

    public string Recipe { get; }

    public double RuinRebuildRatio { get; }

    public string[] Tags { get; }
}

public static class BuildingCatalog
{
    public const string RelativeDir = "buildings";

    public static BuildingDef[] LoadDir(string dir)
    {
        ContentIds.RequireDirectory(dir);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new List<BuildingDef>();
        foreach (string path in ContentIds.EnumerateJsonFiles(dir))
            defs.AddRange(Parse(ContentIds.ReadFile(path), path, seen));

        if (defs.Count == 0)
            throw new InvalidOperationException($"{dir}: expected at least one building def.");
        return defs.ToArray();
    }

    public static BuildingDef[] Parse(string json, string source)
        => Parse(json, source, new HashSet<string>(StringComparer.Ordinal));

    private static BuildingDef[] Parse(string json, string source, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativeDir;
        var docs = ContentIds.ReadDocuments(json, source);
        var defs = new BuildingDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<BuildingDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static BuildingDef Read(BuildingDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        if (doc.Placement is null)
            throw new InvalidOperationException($"{source}: '{id}' placement is required.");
        if (doc.Rotations < 1)
            throw new InvalidOperationException($"{source}: '{id}' rotations must be >= 1.");

        string? recipe = ContentIds.OptionalContentId(doc.Recipe, source);
        if (recipe is null)
            throw new InvalidOperationException($"{source}: '{id}' recipe is required.");

        double ruin = ContentIds.RequireFiniteNonNegative(doc.RuinRebuildRatio, source, id, "ruinRebuildRatio");
        if (ruin > 1)
            throw new InvalidOperationException($"{source}: '{id}' ruinRebuildRatio must be between 0 and 1.");

        return new BuildingDef(
            id,
            ContentIds.RequireName(doc.Name, source, id),
            ContentIds.RequireGrid(doc.Footprint, source, id, "footprint"),
            doc.Rotations,
            ContentIds.RequireHp(doc.Hp, source, id),
            doc.Placement.OnStreet,
            ParseWater(doc.Placement.OnWater, source, id),
            ContentIds.RequireFiniteNonNegative(doc.Placement.MaxSlopeDeg, source, id, "placement.maxSlopeDeg"),
            doc.Placement.DragLine,
            ParseBehaviour(doc.Behaviour, source, id),
            ContentIds.OptionalContentId(doc.Container, source),
            recipe,
            ruin,
            ContentIds.ReadTags(doc.Tags, source, id));
    }

    private static WaterPlacement ParseWater(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(raw, source, $"'{id}' placement.onWater", "none", "shallow", "deep", "shore");
        return token switch
        {
            "none" => WaterPlacement.None,
            "shallow" => WaterPlacement.Shallow,
            "deep" => WaterPlacement.Deep,
            "shore" => WaterPlacement.Shore,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown placement.onWater '{raw}'.")
        };
    }

    private static BuildingBehaviour ParseBehaviour(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(
            raw,
            source,
            $"'{id}' behaviour",
            "belt",
            "pipe",
            "splitter",
            "merger",
            "sorter",
            "inserter",
            "container",
            "wall",
            "gate",
            "spike",
            "turret",
            "alarm",
            "vehicle_depot",
            "port",
            "pump",
            "pier");
        return token switch
        {
            "belt" => BuildingBehaviour.Belt,
            "pipe" => BuildingBehaviour.Pipe,
            "splitter" => BuildingBehaviour.Splitter,
            "merger" => BuildingBehaviour.Merger,
            "sorter" => BuildingBehaviour.Sorter,
            "inserter" => BuildingBehaviour.Inserter,
            "container" => BuildingBehaviour.Container,
            "wall" => BuildingBehaviour.Wall,
            "gate" => BuildingBehaviour.Gate,
            "spike" => BuildingBehaviour.Spike,
            "turret" => BuildingBehaviour.Turret,
            "alarm" => BuildingBehaviour.Alarm,
            "vehicle_depot" => BuildingBehaviour.VehicleDepot,
            "port" => BuildingBehaviour.Port,
            "pump" => BuildingBehaviour.Pump,
            "pier" => BuildingBehaviour.Pier,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown behaviour '{raw}'.")
        };
    }

    private sealed class BuildingDocument
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int[]? Footprint { get; set; }
        public int Rotations { get; set; }
        public int Hp { get; set; }
        public PlacementDocument? Placement { get; set; }
        public string? Behaviour { get; set; }
        public string? Container { get; set; }
        public string? Recipe { get; set; }
        public double RuinRebuildRatio { get; set; }
        public string[]? Tags { get; set; }
    }

    private sealed class PlacementDocument
    {
        public bool OnStreet { get; set; }
        public string? OnWater { get; set; }
        public double MaxSlopeDeg { get; set; }
        public bool DragLine { get; set; }
    }
}
