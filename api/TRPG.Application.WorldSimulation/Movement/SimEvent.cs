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

public sealed record JourneyCompleted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    Guid JobId,
    CreatureJobAction Action,
    Guid? ArrivalNodeId = null
) : SimEvent(CreatureId, At);
