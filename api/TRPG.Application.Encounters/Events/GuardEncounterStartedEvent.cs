using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record GuardEncounterStartedEvent(GuardEncounterState State) : GameClientEvent;
