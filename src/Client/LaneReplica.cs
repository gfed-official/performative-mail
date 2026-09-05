using System.Collections.Generic;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Client;

public sealed class LaneReplica
{
    private readonly Dictionary<(ulong Segment, byte Lane), int> _counts = new();

    public int Count(SegmentId segment, int lane)
    {
        if (lane != 0 && lane != 1)
            return 0;
        return _counts.TryGetValue((segment.Value, (byte)lane), out int n) ? n : 0;
    }

    public void Apply(LaneInsert insert)
    {
        if (insert is null) return;
        if (insert.Lane > 1) return;
        var key = (insert.Segment.Value, insert.Lane);
        _counts.TryGetValue(key, out int n);
        _counts[key] = n + 1;
    }

    public void Apply(LaneRemove remove)
    {
        if (remove is null) return;
        if (remove.Lane > 1) return;
        var key = (remove.Segment.Value, remove.Lane);
        if (!_counts.TryGetValue(key, out int n) || n <= 0)
            return;
        if (n == 1)
            _counts.Remove(key);
        else
            _counts[key] = n - 1;
    }
}
