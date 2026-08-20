using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record HostileEncounterResolvedEvent(HostileEncounterResolutionFact Fact) : GameClientEvent;
