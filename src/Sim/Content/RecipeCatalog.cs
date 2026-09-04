using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PerformativeMail.Sim.Content;

public readonly record struct RecipeInput(string Item, int Count);

public sealed class RecipeDef
{
    public RecipeDef(string id, string producesBuilding, RecipeInput[] inputs, string? blueprint, int unlockShift)
    {
        Id = id;
        ProducesBuilding = producesBuilding;
        Inputs = inputs;
        Blueprint = blueprint;
        UnlockShift = unlockShift;
    }

    public string Id { get; }

    public string ProducesBuilding { get; }

    public RecipeInput[] Inputs { get; }

    public string? Blueprint { get; }

    public int UnlockShift { get; }
}

public static class RecipeCatalog
{
    public const string RelativeDir = "recipes";

    public static RecipeDef[] LoadDir(string dir)
    {
        ContentIds.RequireDirectory(dir);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new List<RecipeDef>();
        foreach (string path in ContentIds.EnumerateJsonFiles(dir))
            defs.AddRange(Parse(ContentIds.ReadFile(path), path, seen));

        if (defs.Count == 0)
            throw new InvalidOperationException($"{dir}: expected at least one recipe def.");
        return defs.ToArray();
    }

    public static RecipeDef[] Parse(string json, string source)
        => Parse(json, source, new HashSet<string>(StringComparer.Ordinal));

    private static RecipeDef[] Parse(string json, string source, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativeDir;
        var docs = ContentIds.ReadDocuments(json, source);
        var defs = new RecipeDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<RecipeDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static RecipeDef Read(RecipeDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        return new RecipeDef(
            id,
            ReadProducesBuilding(doc.Produces, source, id),
            ReadInputs(doc.Inputs, source, id),
            ContentIds.OptionalContentId(doc.Blueprint, source),
            ContentIds.RequireUnlockShift(doc.UnlockShift, source, id));
    }

    private static string ReadProducesBuilding(JsonElement produces, string source, string id)
    {
        if (produces.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new InvalidOperationException($"{source}: '{id}' produces is required.");
        if (produces.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{source}: '{id}' produces must be an object with only building.");

        string? building = null;
        foreach (var prop in produces.EnumerateObject())
        {
            if (prop.Name != "building")
                throw new InvalidOperationException($"{source}: '{id}' produces must contain only building (found '{prop.Name}').");
            if (prop.Value.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException($"{source}: '{id}' produces.building is required.");
            building = ContentIds.OptionalContentId(prop.Value.GetString(), source);
        }

        if (building is null)
            throw new InvalidOperationException($"{source}: '{id}' produces.building is required.");
        return building;
    }

    private static RecipeInput[] ReadInputs(InputDocument[]? inputs, string source, string id)
    {
        if (inputs is null || inputs.Length == 0)
            throw new InvalidOperationException($"{source}: '{id}' inputs is required.");

        var copy = new RecipeInput[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
        {
            var row = inputs[i];
            if (row is null)
                throw new InvalidOperationException($"{source}: '{id}' inputs[{i}] is empty.");
            string? item = ContentIds.OptionalContentId(row.Item, source);
            if (item is null)
                throw new InvalidOperationException($"{source}: '{id}' inputs[{i}] item is required.");
            if (row.Count < 1)
                throw new InvalidOperationException($"{source}: '{id}' inputs[{i}] count must be >= 1.");
            copy[i] = new RecipeInput(item, row.Count);
        }

        return copy;
    }

    private sealed class RecipeDocument
    {
        public string? Id { get; set; }
        public JsonElement Produces { get; set; }
        public InputDocument[]? Inputs { get; set; }
        public string? Blueprint { get; set; }
        public int UnlockShift { get; set; }
    }

    private sealed class InputDocument
    {
        public string? Item { get; set; }
        public int Count { get; set; }
    }
}
