using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record HostileEncounterResolvedEvent(Guid WorldId, HostileEncounterResolutionFact Fact)
    : GameClientEvent(WorldId);
