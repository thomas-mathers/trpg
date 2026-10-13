using Tapper;

namespace TRPG.GameSessions.Responses;

[TranspilationSource]
public record CreaturesArrivedPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    IReadOnlyCollection<CreatureStatusSnapshot> Creatures
);

[TranspilationSource]
public record CreaturesLeftPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    IReadOnlyCollection<Guid> CreatureIds
);

[TranspilationSource]
public record CreaturePlacementSnapshot(Guid CreatureId, PlacementWire Placement);

[TranspilationSource]
public record CreaturesMovedPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    IReadOnlyCollection<CreaturePlacementSnapshot> Placements
);

[TranspilationSource]
public record CreaturesUpdatedPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    IReadOnlyCollection<CreatureStatusSnapshot> Creatures
);

[TranspilationSource]
public record CaravansArrivedPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    IReadOnlyCollection<NearbyCaravanSnapshot> Caravans
);

[TranspilationSource]
public record CaravansLeftPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    IReadOnlyCollection<Guid> CaravanIds
);

[TranspilationSource]
public record CaravansUpdatedPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    IReadOnlyCollection<NearbyCaravanSnapshot> Caravans
);

[TranspilationSource]
public record WeatherChangedPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    WeatherCondition? Weather
);

[TranspilationSource]
public record ClockReanchoredPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    long GameTimeMilliseconds,
    long AnchoredAtUnixMilliseconds,
    double TimeScale
);

[TranspilationSource]
public record PlayerCorrectedPayload(Guid PlayerId, Guid LocationId, double X, double Y);

[TranspilationSource]
public record PropOccupancySnapshot(Guid PropId, bool IsOccupied, bool IsOccupiedByPlayer);

[TranspilationSource]
public record PropOccupancyChangedPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    IReadOnlyCollection<PropOccupancySnapshot> Props
);

[TranspilationSource]
public record SeatingChangedPayload(
    Guid WorldId,
    Guid LocationId,
    long Version,
    Guid CreatureId,
    Guid? SeatId,
    CreaturePosture Posture,
    PlacementWire Placement
);
