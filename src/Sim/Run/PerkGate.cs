using System;
using PerformativeMail.Sim.Content;

namespace PerformativeMail.Sim.Run;

internal static class PerkGate
{
    public static bool MeetsPrerequisites(PerkDef def, byte shift, in DraftRules rules)
    {
        PerkPrerequisites? pre = def.Prerequisites;
        if (pre is null) return true;
        if (pre.ShiftMin is int shiftMin && shift < shiftMin) return false;
        if (pre.RankMin is int rankMin && rules.Rank < rankMin) return false;
        return rules.HasAnyBuilt(pre.BuiltAny);
    }

    public static bool Conflicts(PerkDef a, PerkDef b)
        => Contains(a.Excludes, b.Id) || Contains(b.Excludes, a.Id);

    public static bool ExcludedByRun(PerkDef def, string[] taken, Func<string, PerkDef> lookup)
    {
        for (int i = 0; i < taken.Length; i++)
        {
            if (Contains(def.Excludes, taken[i]))
                return true;
            if (Conflicts(def, lookup(taken[i])))
                return true;
        }

        return false;
    }

    public static int CountEqual(string[] ids, string id)
    {
        int n = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.Ordinal))
                n++;
        }

        return n;
    }

    private static bool Contains(string[] ids, string id)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
