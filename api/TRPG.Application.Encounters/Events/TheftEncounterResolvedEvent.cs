using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record TheftEncounterResolvedEvent(Guid WorldId, TheftEncounterResolutionFact Fact)
    : GameClientEvent(WorldId);
