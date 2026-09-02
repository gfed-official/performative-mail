var contentRoot = args.Length > 0 ? args[0] : "content";

if (!Directory.Exists(contentRoot))
{
    Console.Error.WriteLine($"Content root not found. Path was {contentRoot}");
    return 1;
}

string[] required =
{
    "items",
    "mail",
    "buildings",
    "recipes",
    "perks",
    "enemies",
    "waves",
    "shop",
    "stamps",
};

var missing = required.Where(name => !Directory.Exists(Path.Combine(contentRoot, name))).ToArray();
if (missing.Length > 0)
{
    Console.Error.WriteLine("Missing content directories: " + string.Join(", ", missing));
    return 1;
}

return 0;
