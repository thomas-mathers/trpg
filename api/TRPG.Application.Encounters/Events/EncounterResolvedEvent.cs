using TRPG.Application.Common.Events;
using TRPG.Contracts.Encounters.Responses;

namespace TRPG.Application.Encounters.Events;

internal record EncounterResolvedEvent(EncounterResolutionFact Fact) : GameClientEvent
{
    public override string MethodName => "EncounterResolved";
    public override object? Payload => Fact;
}
