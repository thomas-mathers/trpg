namespace TRPG.Application.Combat.ClientEvents;

public record CombatUpdatePayload(
    IReadOnlyCollection<CombatantState> Combatants,
    IReadOnlyList<CombatRoundEvent> Events,
    CombatOutcome Outcome
);
