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

public record CombatantState(
    Guid Id,
    string Name,
    bool IsPlayer,
    int CurrentHp,
    int MaximumHp,
    bool IsAlive,
    IReadOnlyList<string> Abilities,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions
);

public record CombatState(
    CombatOutcome Outcome,
    IReadOnlyList<CombatantState> Combatants,
    IReadOnlyList<CombatEvent> Events,
    int? XpGained,
    int? GoldLooted,
    IReadOnlyDictionary<WeaponType, int> WeaponSwingCounts
);
