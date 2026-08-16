using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record EncounterStartedEvent(HostileEncounterState State) : GameClientEvent
{
    public override string MethodName => "EncounterStarted";
}
