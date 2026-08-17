namespace TRPG.Combat.ClientModels;

[Tapper.TranspilationSource]
public record CombatUpdatePayload(
    IReadOnlyCollection<CombatantState> Combatants,
    IReadOnlyList<CombatRoundEntry> Events,
    CombatOutcome Outcome
);
