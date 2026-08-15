using TRPG.Application.Common.Events;
using TRPG.Contracts.Encounters.Responses;

namespace TRPG.Application.Encounters.Events;

internal record EncounterStartedEvent(HostileEncounterState State) : GameClientEvent
{
    public override string MethodName => "EncounterStarted";
    public override object? Payload => State;
}
