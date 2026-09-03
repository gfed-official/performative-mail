using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PerformativeMail.Sim.World;

public static class StreetCatalog
{
    public const string RelativePath = "streets.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string[] Load()
    {
        string path = Find() ?? throw new InvalidOperationException("content/streets.json was not found.");
        return LoadFile(path);
    }

    public static string[] LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Could not read '{path}'.", ex);
        }

        return Parse(json, path);
    }

    public static string[] Parse(string json, string source)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        if (string.IsNullOrWhiteSpace(source)) source = RelativePath;

        StreetDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<StreetDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{source}: invalid JSON. {ex.Message}", ex);
        }

        if (doc?.Names is null || doc.Names.Length == 0)
            throw new InvalidOperationException($"{source}: names must be a non-empty array.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var names = new string[doc.Names.Length];
        for (int i = 0; i < doc.Names.Length; i++)
        {
            string? name = doc.Names[i];
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"{source}: names[{i}] is empty.");
            name = name.Trim();
            if (!seen.Add(name))
                throw new InvalidOperationException($"{source}: duplicate name '{name}'.");
            names[i] = name;
        }

        return names;
    }

    public static string? Find()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content", RelativePath);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        return null;
    }

    private sealed class StreetDocument
    {
        [JsonPropertyName("names")]
        public string[]? Names { get; set; }
    }
}
