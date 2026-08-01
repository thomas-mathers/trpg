namespace TRPG.Application.Combat;

public abstract record GameTurnEvent;

public record CombatStartedEvent(IReadOnlyList<Combatant> Combatants) : GameTurnEvent;

public record CombatEndedEvent : GameTurnEvent;
