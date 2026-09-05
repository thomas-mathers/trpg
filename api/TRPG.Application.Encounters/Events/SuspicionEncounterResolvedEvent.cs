using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record SuspicionEncounterResolvedEvent(SuspicionEncounterResolutionFact Fact)
    : GameClientEvent;
