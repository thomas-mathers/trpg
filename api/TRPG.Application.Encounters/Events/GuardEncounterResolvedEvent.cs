using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record GuardEncounterResolvedEvent(Guid WorldId, GuardEncounterResolutionFact Fact)
    : GameClientEvent(WorldId);
