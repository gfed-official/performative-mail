using System.Collections.Generic;

namespace PerformativeMail.Sim.Content;

public sealed class StatSheet
{
    private readonly Dictionary<Stat, double> _bases = new();
    private readonly List<StatModifier> _modifiers = new();

    public void SetBase(Stat stat, double value) => _bases[stat] = value;

    public void Add(StatModifier modifier) => _modifiers.Add(modifier);

    public double Get(Stat stat)
    {
        _bases.TryGetValue(stat, out double basis);
        double mul = 1;
        double add = 0;
        for (int i = 0; i < _modifiers.Count; i++)
        {
            StatModifier modifier = _modifiers[i];
            if (modifier.Stat != stat)
                continue;
            if (modifier.Op == StatOp.Mul)
                mul *= modifier.Value;
            else
                add += modifier.Value;
        }

        return basis * mul + add;
    }
}
