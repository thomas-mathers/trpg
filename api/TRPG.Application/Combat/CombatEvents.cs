using TRPG.Application.Common.Events;
using TRPG.Application.Common.Mappers;
using TRPG.Contracts.Combat.Responses;

namespace TRPG.Application.Combat;

public record CombatStartedEvent(FightState FightState) : GameClientEvent
{
    public override string MethodName => "CombatStarted";
    public override object? Payload => FightState;
}

public record CombatUpdatedEvent(
    FightState FightState,
    IReadOnlyList<CombatRoundEvent> Events,
    TRPG.Data.Models.CombatOutcome? Outcome = null
) : GameClientEvent
{
    public override string MethodName => "CombatUpdated";
    public override object? Payload =>
        new CombatUpdatePayload(FightState, Events, Outcome?.ToContract());
}
