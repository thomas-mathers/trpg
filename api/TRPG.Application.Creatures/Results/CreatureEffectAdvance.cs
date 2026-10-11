using TRPG.Application.Creatures.Commands;
using TRPG.Domain;

namespace TRPG.Application.Creatures.Results;

public sealed record CreatureEffectAdvance(
    Guid LocationId,
    CreatureStateUpdate Update,
    CreatureVitals Vitals
);
