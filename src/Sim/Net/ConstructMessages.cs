using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Net;

public readonly record struct PlaceConstructRequest(
    uint ReqId,
    string BuildingId,
    int TileX,
    int TileY,
    Facing Rotation);

public readonly record struct PlaceConstructConfirmed(
    uint ReqId,
    EntityId ConstructId,
    string BuildingId,
    int TileX,
    int TileY,
    Facing Rotation,
    EntityId Owner);

public readonly record struct RemoveConstructRequest(
    uint ReqId,
    EntityId ConstructId);

public readonly record struct RemoveConstructConfirmed(
    uint ReqId,
    EntityId ConstructId);
