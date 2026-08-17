namespace TRPG.Combat.ClientModels;

[Tapper.TranspilationSource]
public record CombatUpdatePayload(
    IReadOnlyCollection<CombatantState> Combatants,
    IReadOnlyList<CombatActionResult> Actions,
    IReadOnlyList<CombatRegeneration> Regenerations,
    IReadOnlyList<CombatResourceState> ResourceStates,
    CombatOutcome Outcome
);
