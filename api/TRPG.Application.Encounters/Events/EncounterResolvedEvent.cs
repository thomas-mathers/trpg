using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record EncounterResolvedEvent(HostileEncounterResolutionFact Fact) : GameClientEvent;
