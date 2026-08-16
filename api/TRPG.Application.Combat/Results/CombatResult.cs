using TRPG.Application.Abilities;
using TRPG.Application.Combat.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Results;

public record CombatResultPlayerState(
    string Name,
    int CurrentHp,
    int MaximumHp,
    IReadOnlyList<string> Abilities,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions
);

public record CombatResultEnemyState(
    string Name,
    int CurrentHp,
    int MaximumHp,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions
);

public record CombatResult(
    CombatOutcome Outcome,
    CombatResultPlayerState Player,
    IReadOnlyList<CombatResultEnemyState> Enemies,
    IReadOnlyList<CombatResolution> Events
);
