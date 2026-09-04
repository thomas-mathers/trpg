namespace TRPG.Combat.Responses;

[Tapper.TranspilationSource]
public record CombatUpdated(
    IReadOnlyCollection<CombatantState> Combatants,
    IReadOnlyList<CombatActionResult> Actions,
    IReadOnlyList<string> Messages,
    IReadOnlyList<CombatRegeneration> Regenerations,
    IReadOnlyList<CombatResourceState> ResourceStates,
    CombatOutcome Outcome
);
