using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public enum CombatOutcome
{
    Ongoing,
    Victory,
    Defeat,
    Fled,
}

public record PlayerCombatState(
    string Name,
    int CurrentHp,
    int MaximumHp,
    IReadOnlyList<string> Abilities,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions
);

public record EnemyCombatState(
    string Name,
    int CurrentHp,
    int MaximumHp,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions
);

public record CombatState(
    CombatOutcome Outcome,
    PlayerCombatState Player,
    IReadOnlyList<EnemyCombatState> Enemies,
    IReadOnlyList<CombatEvent> Events,
    int? XpGained,
    int? GoldLooted,
    IReadOnlyDictionary<WeaponType, int> WeaponSwingCounts
);
