namespace TRPG.Application.Combat.Responses;

public record CombatUpdatePayload(
    IReadOnlyCollection<CombatantState> Combatants,
    IReadOnlyList<CombatRoundEvent> Events,
    CombatOutcome Outcome
);
