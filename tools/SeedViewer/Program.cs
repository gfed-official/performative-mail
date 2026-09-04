using System.Globalization;
using PerformativeMail.Sim.World;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: SeedViewer <seed>");
    Console.Error.WriteLine("Seed is a decimal or 0x-prefixed hex uint32.");
    return 2;
}

if (!TryParseSeed(args[0], out uint seed))
{
    Console.Error.WriteLine($"Invalid seed '{args[0]}'.");
    return 2;
}

var tables = WorldHashCheck.Regenerate(seed);
ulong hash = WorldHash.Compute(tables);
Console.Write(SeedView.Render(seed, tables, hash));
return tables.Valid ? 0 : 1;

static bool TryParseSeed(string text, out uint seed)
{
    seed = 0;
    if (string.IsNullOrWhiteSpace(text))
        return false;

    text = text.Trim();
    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return uint.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out seed);

    return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed);
}
