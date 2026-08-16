using TRPG.Application.Abilities;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Results;

public record CombatantResult(
    Guid Id,
    string Name,
    int Level,
    bool IsPlayer,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    bool IsAlive,
    IReadOnlyList<string> Abilities,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions,
    IReadOnlyCollection<CombatDotState> ActiveDots,
    IReadOnlyCollection<CombatHotState> ActiveHots,
    IReadOnlyCollection<CombatBuffState> ActiveBuffs,
    IReadOnlyDictionary<Guid, int> ItemsUsedCounts
);

public record CombatDotState(
    string AbilityName,
    int Amount,
    DamageType DamageType,
    int RemainingTurns
);

public record CombatHotState(string AbilityName, int Amount, int RemainingTurns);

public record CombatBuffState(
    string AbilityName,
    AttributeName Attribute,
    float Amount,
    AmountType AmountType,
    int RemainingTurns
);
