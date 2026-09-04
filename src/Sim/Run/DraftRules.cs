using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Run;

public readonly struct DraftRules
{
    public static DraftRules None { get; } = new(Array.Empty<string>(), 1);

    private readonly HashSet<string> _built;

    public DraftRules(IReadOnlyCollection<string> built, int rank = 1)
    {
        if (rank < 1)
            throw new ArgumentOutOfRangeException(nameof(rank), rank, null);

        _built = new HashSet<string>(StringComparer.Ordinal);
        if (built != null)
        {
            foreach (string id in built)
            {
                if (!string.IsNullOrEmpty(id))
                    _built.Add(id);
            }
        }

        Rank = rank;
    }

    public int Rank { get; }

    public bool HasAnyBuilt(string[] ids)
    {
        if (ids is null || ids.Length == 0) return true;
        if (_built is null) return false;
        for (int i = 0; i < ids.Length; i++)
        {
            if (_built.Contains(ids[i]))
                return true;
        }

        return false;
    }
}
