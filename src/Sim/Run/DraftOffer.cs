using System;

namespace PerformativeMail.Sim.Run;

public readonly record struct DraftOffer
{
    public DraftOffer(string first, string second, string third)
    {
        if (string.IsNullOrEmpty(first)) throw new ArgumentException("Card id is required.", nameof(first));
        if (string.IsNullOrEmpty(second)) throw new ArgumentException("Card id is required.", nameof(second));
        if (string.IsNullOrEmpty(third)) throw new ArgumentException("Card id is required.", nameof(third));
        if (string.Equals(first, second, StringComparison.Ordinal)
            || string.Equals(first, third, StringComparison.Ordinal)
            || string.Equals(second, third, StringComparison.Ordinal))
            throw new ArgumentException("Draft cards must be distinct.");

        First = first;
        Second = second;
        Third = third;
    }

    public string First { get; }

    public string Second { get; }

    public string Third { get; }
}
