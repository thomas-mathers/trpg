namespace TRPG.Combat.ClientModels;

public record CombatUpdatePayload(
    IReadOnlyCollection<CombatantState> Combatants,
    IReadOnlyList<CombatRoundEntry> Events,
    CombatOutcome Outcome
);
