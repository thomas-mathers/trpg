using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record TrapEncounterResolvedEvent(Guid WorldId, TrapEncounterResolutionFact Fact)
    : GameClientEvent(WorldId);
