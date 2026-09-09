using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record TrapEncounterResolvedEvent(TrapEncounterResolutionFact Fact) : GameClientEvent;
