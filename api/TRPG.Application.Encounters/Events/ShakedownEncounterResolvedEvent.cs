using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record ShakedownEncounterResolvedEvent(Guid WorldId, ShakedownEncounterResolutionFact Fact)
    : GameClientEvent(WorldId);
