using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record ShakedownEncounterResolvedEvent(ShakedownEncounterResolutionFact Fact)
    : GameClientEvent;
