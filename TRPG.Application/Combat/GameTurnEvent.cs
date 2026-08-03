using TRPG.Application.GameSessions;
using TRPG.Contracts.Combat.Responses;

namespace TRPG.Application.Combat;

public record CombatStartedEvent(FightState FightState) : GameTurnEvent
{
    public override string MethodName => "CombatStarted";
    public override object? Payload => FightState;
}

public record CombatUpdatedEvent(FightState FightState) : GameTurnEvent
{
    public override string MethodName => "CombatUpdated";
    public override object? Payload => FightState;
}

public record CombatEndedEvent : GameTurnEvent
{
    public override string MethodName => "CombatEnded";
}
