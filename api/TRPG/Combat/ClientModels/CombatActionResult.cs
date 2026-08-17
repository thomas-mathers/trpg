using Tapper;

namespace TRPG.Combat.ClientModels;

[TranspilationSource]
public enum CombatActionOutcome
{
    Hit,
    Miss,
    Block,
}

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
    string Narration
);
