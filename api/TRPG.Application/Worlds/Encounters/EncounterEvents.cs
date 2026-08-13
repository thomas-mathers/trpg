using TRPG.Application.GameSessions;
using TRPG.Contracts.Encounters.Responses;

namespace TRPG.Application.Worlds.Encounters;

public record EncounterStartedEvent(HostileEncounterState State) : GameClientEvent
{
    public override string MethodName => "EncounterStarted";
    public override object? Payload => State;
}

public record EncounterResolvedEvent(EncounterResolutionFact Fact) : GameClientEvent
{
    public override string MethodName => "EncounterResolved";
    public override object? Payload => Fact;
}
