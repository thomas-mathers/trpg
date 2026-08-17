namespace TRPG.Combat.Responses;

[Tapper.TranspilationSource]
public record CombatUpdatePayload(
    IReadOnlyCollection<CombatantState> Combatants,
    IReadOnlyList<CombatActionResult> Actions,
    IReadOnlyList<CombatRegeneration> Regenerations,
    IReadOnlyList<CombatResourceState> ResourceStates,
    CombatOutcome Outcome
);
