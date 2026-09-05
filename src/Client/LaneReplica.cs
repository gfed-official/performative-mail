using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Client;

public sealed class LaneReplica
{
    private readonly Dictionary<(ulong Segment, byte Lane), List<LaneStateItem>> _lanes = new();
    private readonly HashSet<ulong> _live = new();

    public bool HasSegment(SegmentId segment) => _live.Contains(segment.Value);

    public int Count(SegmentId segment, int lane)
    {
        if (!TryList(segment, lane, out var items))
            return 0;
        return items.Count;
    }

    public IReadOnlyList<int> Positions(SegmentId segment, int lane)
    {
        if (!TryList(segment, lane, out var items))
            return Array.Empty<int>();

        var rows = new int[items.Count];
        for (int i = 0; i < items.Count; i++)
            rows[i] = items[i].PositionCm;
        return rows;
    }

    public IReadOnlyList<int> DrawPositions(SegmentId segment, int lane, int lengthCm)
    {
        if (!TryList(segment, lane, out var items))
            return Array.Empty<int>();

        var drawn = new List<int>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            int at = items[i].PositionCm;
            if (at < lengthCm)
                drawn.Add(at);
        }

        return drawn;
    }

    public bool Advance(SegmentId segment, float dt, float metresPerSecond, int lengthCm = int.MaxValue)
    {
        if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt), dt, null);
        if (metresPerSecond < 0f)
            throw new ArgumentOutOfRangeException(nameof(metresPerSecond), metresPerSecond, null);

        int delta = (int)MathF.Round(metresPerSecond * dt * BeltNetwork.MetresToCm);
        bool lane0 = AdvanceLane(segment, 0, delta, lengthCm);
        bool lane1 = AdvanceLane(segment, 1, delta, lengthCm);
        return lane0 || lane1;
    }

    public void Drop(SegmentId segment)
    {
        _live.Remove(segment.Value);
        _lanes.Remove((segment.Value, 0));
        _lanes.Remove((segment.Value, 1));
    }

    public bool Matches(LaneChecksum checksum)
    {
        if (checksum is null) return false;
        if (!TryList(checksum.Segment, checksum.Lane, out var items))
            return checksum.Count == 0 && checksum.Hash == LaneHash.Of(Array.Empty<int>());

        if (items.Count != checksum.Count) return false;
        return HashOf(items) == checksum.Hash;
    }

    public void Apply(LaneInsert insert)
    {
        if (insert is null) return;
        if (insert.Lane > 1) return;
        var items = GetOrCreate(insert.Segment, insert.Lane);
        items.Add(new LaneStateItem(0, insert.PositionAtTickCm));
        items.Sort(HeadFirst);
        Touch(insert.Segment);
    }

    public void Apply(LaneRemove remove)
    {
        if (remove is null) return;
        if (remove.Lane > 1) return;
        var key = (remove.Segment.Value, remove.Lane);
        if (!_lanes.TryGetValue(key, out var items) || items.Count == 0)
            return;
        items.RemoveAt(0);
        if (items.Count == 0)
            _lanes.Remove(key);
    }

    public void Apply(LaneState state)
    {
        if (state is null || state.Items is null) return;
        if (state.Lane > 1) return;
        Touch(state.Segment);
        var key = (state.Segment.Value, state.Lane);
        if (state.Items.Length == 0)
        {
            _lanes.Remove(key);
            return;
        }

        var items = new List<LaneStateItem>(state.Items.Length);
        for (int i = 0; i < state.Items.Length; i++)
            items.Add(state.Items[i]);
        items.Sort(HeadFirst);
        _lanes[key] = items;
    }

    public bool TryPlantDrift(SegmentId segment, int lane, int deltaCm)
    {
        if (!TryList(segment, lane, out var items) || items.Count == 0)
            return false;
        var head = items[0];
        items[0] = new LaneStateItem(head.MailId, head.PositionCm + deltaCm);
        items.Sort(HeadFirst);
        return true;
    }

    private bool AdvanceLane(SegmentId segment, byte lane, int deltaCm, int lengthCm)
    {
        if (!TryList(segment, lane, out var items) || items.Count == 0)
            return false;

        for (int i = 0; i < items.Count; i++)
        {
            var row = items[i];
            int next = row.PositionCm + deltaCm;
            if (next > lengthCm) next = lengthCm;
            if (next < 0) next = 0;
            items[i] = new LaneStateItem(row.MailId, next);
        }

        items.Sort(HeadFirst);
        return true;
    }

    private void Touch(SegmentId segment) => _live.Add(segment.Value);

    private List<LaneStateItem> GetOrCreate(SegmentId segment, byte lane)
    {
        var key = (segment.Value, lane);
        if (_lanes.TryGetValue(key, out var items))
            return items;
        items = new List<LaneStateItem>();
        _lanes[key] = items;
        return items;
    }

    private bool TryList(SegmentId segment, int lane, out List<LaneStateItem> items)
    {
        items = null!;
        if (lane != 0 && lane != 1)
            return false;
        return _lanes.TryGetValue((segment.Value, (byte)lane), out items!);
    }

    private static uint HashOf(List<LaneStateItem> items)
    {
        var positions = new int[items.Count];
        for (int i = 0; i < items.Count; i++)
            positions[i] = items[i].PositionCm;
        return LaneHash.Of(positions);
    }

    private static int HeadFirst(LaneStateItem a, LaneStateItem b) =>
        b.PositionCm.CompareTo(a.PositionCm);
}
