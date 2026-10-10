using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

public abstract record SimEvent(Guid CreatureId, GameInstant At);

public sealed record JourneyCheckpoint(
    Guid CreatureId,
    GameInstant At,
    Guid JourneyId,
    JourneyStatus Status,
    int LegIndex,
    double LegProgressMeters,
    GameInstant? PausedAt = null
) : SimEvent(CreatureId, At);

public sealed record JourneyStarted(
    Guid CreatureId,
    GameInstant At,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    Guid? NextConnectorId,
    Point? StopPosition = null
) : SimEvent(CreatureId, At);

public sealed record LocationEntered(
    Guid CreatureId,
    GameInstant At,
    Guid FromLocationId,
    Guid ToLocationId,
    Guid ConnectorId,
    Guid? NextConnectorId,
    Point? StopPosition = null,
    Guid? ArrivalNodeId = null
) : SimEvent(CreatureId, At);

public sealed record JourneyLegCompleted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    Point StopPosition,
    Guid ArrivalNodeId
) : SimEvent(CreatureId, At);

public sealed record JourneyCompleted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    Guid JobId,
    CreatureJobAction Action,
    Guid? ArrivalNodeId = null,
    Guid? JourneyId = null
) : SimEvent(CreatureId, At)
{
    public Point? StopPosition { get; init; }
}

public sealed record LocalMoveStarted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    LocalMovePlan Move,
    double MetersPerGameSecond
) : SimEvent(CreatureId, At);

public sealed record LocalMoveCompleted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    LocalMovePlan Move,
    Point StopPosition
) : SimEvent(CreatureId, At);

public sealed record LocalMoveInterrupted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    Point StopPosition
) : SimEvent(CreatureId, At);
