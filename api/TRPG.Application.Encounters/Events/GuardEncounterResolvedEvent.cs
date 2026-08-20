using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record GuardEncounterResolvedEvent(GuardEncounterResolutionFact Fact) : GameClientEvent;
