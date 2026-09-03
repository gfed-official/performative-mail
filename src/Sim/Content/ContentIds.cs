using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Content;

internal static class ContentIds
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static bool IsContentId(string id)
    {
        if (id.Length == 0) return false;
        if (id[0] is < 'a' or > 'z') return false;
        for (int i = 1; i < id.Length; i++)
        {
            char c = id[i];
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_') continue;
            return false;
        }

        return true;
    }

    public static string ReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Could not read '{path}'.", ex);
        }
    }

    public static void RequireDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) throw new ArgumentException("Directory is required.", nameof(dir));
        if (!Directory.Exists(dir))
            throw new InvalidOperationException($"Content directory not found. Path was {dir}");
    }

    public static string[] EnumerateJsonFiles(string dir, string? skipFileName = null)
    {
        var files = Directory.GetFiles(dir, "*.json");
        Array.Sort(files, StringComparer.Ordinal);
        if (skipFileName is null) return files;

        var kept = new List<string>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            if (string.Equals(Path.GetFileName(files[i]), skipFileName, StringComparison.OrdinalIgnoreCase))
                continue;
            kept.Add(files[i]);
        }

        return kept.ToArray();
    }

    public static JsonElement[] ReadDocuments(string json, string source)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{source}: invalid JSON. {ex.Message}", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
                return new[] { root.Clone() };
            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = new JsonElement[root.GetArrayLength()];
                int i = 0;
                foreach (var el in root.EnumerateArray())
                    list[i++] = el.Clone();
                return list;
            }
        }

        throw new InvalidOperationException($"{source}: expected an object or an array of objects.");
    }

    public static T DeserializeObject<T>(string json, string source)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{source}: invalid JSON. {ex.Message}", ex);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"{source}: expected a single object.");
            return Deserialize<T>(doc.RootElement.Clone(), source, 0);
        }
    }

    public static T Deserialize<T>(JsonElement element, string source, int index)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw new InvalidOperationException($"{source}: defs[{index}] is empty.");
        try
        {
            var value = element.Deserialize<T>(JsonOptions);
            if (value is null)
                throw new InvalidOperationException($"{source}: defs[{index}] is empty.");
            return value;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{source}: invalid JSON. {ex.Message}", ex);
        }
    }

    public static string RequireId(string? id, string source, int index)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException($"{source}: defs[{index}] id is required.");
        id = id.Trim();
        if (!IsContentId(id))
            throw new InvalidOperationException($"{source}: unknown id '{id}'. Ids are lowercase snake_case.");
        return id;
    }

    public static void AddUnique(HashSet<string> seen, string id, string source)
    {
        if (!seen.Add(id))
            throw new InvalidOperationException($"{source}: duplicate id '{id}'.");
    }

    public static string RequireName(string? name, string source, string id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"{source}: '{id}' name is required.");
        return name.Trim();
    }

    public static Footprint RequireGrid(int[]? value, string source, string id, string field)
    {
        if (value is null || value.Length != 2)
            throw new InvalidOperationException($"{source}: '{id}' {field} must be [cols, rows].");
        int cols = value[0];
        int rows = value[1];
        if (cols <= 0 || rows <= 0 || cols > 255 || rows > 255)
            throw new InvalidOperationException($"{source}: '{id}' {field} must be two positive integers <= 255.");
        return new Footprint((byte)cols, (byte)rows);
    }

    public static WeightClass ParseWeight(string? raw, string source, string id)
    {
        string token = RequireClosed(raw, source, $"'{id}' weightClass", "light", "medium", "heavy", "bulk");
        return token switch
        {
            "light" => WeightClass.Light,
            "medium" => WeightClass.Medium,
            "heavy" => WeightClass.Heavy,
            "bulk" => WeightClass.Bulk,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown weightClass '{raw}'.")
        };
    }

    public static StackCategory ParseItemCategory(string? raw, string source, string id)
    {
        string token = RequireClosed(
            raw,
            source,
            $"'{id}' category",
            "tool",
            "material",
            "consumable",
            "ammo",
            "blueprint",
            "weapon");
        return token switch
        {
            "tool" => StackCategory.Tool,
            "material" => StackCategory.Material,
            "consumable" => StackCategory.Consumable,
            "ammo" => StackCategory.Ammo,
            "blueprint" => StackCategory.Blueprint,
            "weapon" => StackCategory.Weapon,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown category '{raw}'.")
        };
    }

    public static StackCategory ParseStackCategory(string? raw, string source, string id, int index)
    {
        string token = RequireClosed(
            raw,
            source,
            $"'{id}' allowedCategories[{index}]",
            "mail",
            "tool",
            "material",
            "consumable",
            "ammo",
            "blueprint",
            "weapon");
        return token switch
        {
            "mail" => StackCategory.Mail,
            "tool" => StackCategory.Tool,
            "material" => StackCategory.Material,
            "consumable" => StackCategory.Consumable,
            "ammo" => StackCategory.Ammo,
            "blueprint" => StackCategory.Blueprint,
            "weapon" => StackCategory.Weapon,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown category '{raw}'.")
        };
    }

    public static string RequireClosed(string? raw, string source, string field, params string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException($"{source}: {field} is required.");
        raw = raw.Trim();
        for (int i = 0; i < allowed.Length; i++)
        {
            if (raw == allowed[i]) return raw;
        }

        throw new InvalidOperationException($"{source}: unknown {field} '{raw}'.");
    }

    public static int RequireMaxStack(int value, string source, string id)
    {
        if (value < 1)
            throw new InvalidOperationException($"{source}: '{id}' maxStack must be >= 1.");
        return value;
    }

    public static int RequirePrice(int value, string source, string id, string field)
    {
        if (value < 0)
            throw new InvalidOperationException($"{source}: '{id}' {field} must be >= 0.");
        return value;
    }

    public static int RequireUnlockShift(int value, string source, string id)
    {
        if (value < 1)
            throw new InvalidOperationException($"{source}: '{id}' unlockShift must be >= 1.");
        return value;
    }

    public static int RequireHp(int value, string source, string id)
    {
        if (value <= 0)
            throw new InvalidOperationException($"{source}: '{id}' hp must be > 0.");
        return value;
    }

    public static int RequireNonNegative(int value, string source, string id, string field)
    {
        if (value < 0)
            throw new InvalidOperationException($"{source}: '{id}' {field} must be >= 0.");
        return value;
    }

    public static double RequireFiniteNonNegative(double value, string source, string id, string field)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw new InvalidOperationException($"{source}: '{id}' {field} must be a finite non-negative number.");
        return value;
    }

    public static string? OptionalContentId(string? raw, string source)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (!IsContentId(raw))
            throw new InvalidOperationException($"{source}: unknown id '{raw}'. Ids are lowercase snake_case.");
        return raw;
    }

    public static string[] ReadTags(string[]? tags, string source, string id)
    {
        if (tags is null || tags.Length == 0) return Array.Empty<string>();
        var copy = new string[tags.Length];
        for (int i = 0; i < tags.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(tags[i]))
                throw new InvalidOperationException($"{source}: '{id}' tags[{i}] is empty.");
            copy[i] = tags[i].Trim();
        }

        return copy;
    }

    public static string[] ReadIdList(string[]? values, string source, string id, string field, bool required)
    {
        if (values is null)
        {
            if (required)
                throw new InvalidOperationException($"{source}: '{id}' {field} is required.");
            return Array.Empty<string>();
        }

        var copy = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
                throw new InvalidOperationException($"{source}: '{id}' {field}[{i}] is empty.");
            string token = values[i].Trim();
            if (!IsContentId(token))
                throw new InvalidOperationException($"{source}: unknown id '{token}'. Ids are lowercase snake_case.");
            copy[i] = token;
        }

        return copy;
    }
}
