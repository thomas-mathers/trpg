using TRPG.Domain;

namespace TRPG.Application.Creatures.Results;

public sealed record CreatureVitalState(
    CreatureVitals Vitals,
    Guid LocationId,
    GameInstant LastRegenAt
);
