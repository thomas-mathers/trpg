using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public record CombatantState(
    Guid Id,
    string Name,
    bool IsPlayer,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int CurrentMp,
    bool IsAlive,
    IReadOnlyList<string> Abilities,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions,
    IReadOnlyDictionary<Guid, int> ItemsUsedCounts
);

public record CombatState(
    CombatOutcome Outcome,
    IReadOnlyList<CombatantState> Combatants,
    IReadOnlyList<CombatEvent> Events,
    IReadOnlyDictionary<WeaponType, int> WeaponSwingCounts,
    IReadOnlyDictionary<Skill, int> SkillUsageCounts
);
