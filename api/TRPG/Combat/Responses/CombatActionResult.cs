using Tapper;

namespace TRPG.Combat.Responses;

[TranspilationSource]
public enum CombatActionOutcome
{
    Hit,
    Miss,
    Block,
    Heal,
    HealOverTime,
    Buff,
    ConsumePotion,
    FleeFailed,
}

[TranspilationSource]
public record CombatActionBuffModifier(
    AttributeName Attribute,
    float Amount,
    AmountType AmountType,
    int DurationSeconds
);

[TranspilationSource]
public record CombatActionResult(
    CombatActionOutcome Outcome,
    Guid AttackerId,
    string AttackerName,
    string AbilityName,
    Guid TargetId,
    string TargetName,
    int? Damage,
    DamageType? DamageType,
    bool? IsCritical,
    bool? Killed,
    int? TargetRemainingHp,
    int? TargetMaximumHp,
    IReadOnlyCollection<ConditionType>? AppliedConditions,
    IReadOnlyCollection<CombatActionBuffModifier>? AppliedBuffs,
    int? HotAmountPerTick,
    int? HotDurationSeconds,
    string Narration
);
