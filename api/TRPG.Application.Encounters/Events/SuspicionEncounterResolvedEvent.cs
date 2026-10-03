using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record SuspicionEncounterResolvedEvent(Guid WorldId, SuspicionEncounterResolutionFact Fact)
    : GameClientEvent(WorldId);
