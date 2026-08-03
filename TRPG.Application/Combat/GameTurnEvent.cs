using TRPG.Application.Common.Mappers;
using TRPG.Application.GameSessions;
using TRPG.Contracts.Combat.Responses;

namespace TRPG.Application.Combat;

public record CombatStartedEvent(FightState FightState) : GameTurnEvent
{
    public override string MethodName => "CombatStarted";
    public override object? Payload => FightState;
}

public record CombatUpdatedEvent(FightState FightState, IReadOnlyList<CombatRoundEvent> Events)
    : GameTurnEvent
{
    public override string MethodName => "CombatUpdated";
    public override object? Payload => new CombatUpdatePayload(FightState, Events);
}

public record CombatEndedEvent(TRPG.Data.Models.CombatOutcome Outcome) : GameTurnEvent
{
    public override string MethodName => "CombatEnded";
    public override object? Payload => Outcome.ToContract();
}
