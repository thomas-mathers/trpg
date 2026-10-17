using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Weather.Results;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

public abstract record SimEvent(GameInstant At);

public abstract record CreatureSimEvent(Guid CreatureId, GameInstant At) : SimEvent(At);

public sealed record JourneyCheckpoint(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    Guid JourneyId,
    JourneyStatus Status,
    int LegIndex,
    double LegProgressMeters,
    GameInstant? PausedAt = null
) : CreatureSimEvent(CreatureId, At);

public sealed record JourneyStarted(
    Guid CreatureId,
    GameInstant At,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    Guid? NextConnectorId,
    Point? StopPosition = null
) : CreatureSimEvent(CreatureId, At);

public sealed record LocationEntered(
    Guid CreatureId,
    GameInstant At,
    Guid FromLocationId,
    Guid ToLocationId,
    Guid ConnectorId,
    Guid? NextConnectorId,
    Point? StopPosition = null,
    Guid? ArrivalNodeId = null
) : CreatureSimEvent(CreatureId, At);

public sealed record JourneyLegCompleted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    Point StopPosition,
    Guid ArrivalNodeId
) : CreatureSimEvent(CreatureId, At);

public sealed record JourneyCompleted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    Guid JobId,
    CreatureJobAction Action,
    Guid? ArrivalNodeId = null,
    Guid? JourneyId = null
) : CreatureSimEvent(CreatureId, At)
{
    public Point? StopPosition { get; init; }
}

public sealed record LocalMoveStarted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    LocalMovePlan Move,
    double MetersPerGameSecond
) : CreatureSimEvent(CreatureId, At);

public sealed record LocalMoveCompleted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    LocalMovePlan Move,
    Point StopPosition
) : CreatureSimEvent(CreatureId, At);

public sealed record LocalMoveInterrupted(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    Point StopPosition,
    double? Heading = null
) : CreatureSimEvent(CreatureId, At);

public abstract record CreatureVitalsEvent(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    CreatureVitals Vitals
) : CreatureSimEvent(CreatureId, At);

public sealed record CreatureRegenerated(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    CreatureVitals Vitals,
    GameInstant LastRegenAt
) : CreatureVitalsEvent(CreatureId, At, LocationId, Vitals);

public sealed record CreatureEffectsAdvanced(
    Guid CreatureId,
    GameInstant At,
    Guid LocationId,
    CreatureVitals Vitals,
    CreatureStateUpdate Update
) : CreatureVitalsEvent(CreatureId, At, LocationId, Vitals);

public sealed record WeatherChanged(GameInstant At, WeatherForecast Forecast) : SimEvent(At);
