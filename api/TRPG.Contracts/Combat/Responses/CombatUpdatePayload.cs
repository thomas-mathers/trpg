namespace TRPG.Contracts.Combat.Responses;

public record CombatUpdatePayload(
    FightState FightState,
    IReadOnlyList<CombatRoundEvent> Events,
    CombatOutcome? Outcome = null
);
