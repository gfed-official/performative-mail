using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Automation;

public abstract record LaneDelta(SegmentId Segment, byte Lane);

public sealed record LaneInsert(
    SegmentId Segment,
    byte Lane,
    MailKindId ItemKind,
    AddressColour Colour,
    int PositionAtTickCm) : LaneDelta(Segment, Lane);

public sealed record LaneRemove(
    SegmentId Segment,
    byte Lane) : LaneDelta(Segment, Lane);

public sealed record LaneChecksum(
    SegmentId Segment,
    byte Lane,
    ushort Count,
    uint Hash);

public readonly record struct LaneStateItem(int MailId, int PositionCm);

public sealed record LaneState(
    SegmentId Segment,
    byte Lane,
    LaneStateItem[] Items);
