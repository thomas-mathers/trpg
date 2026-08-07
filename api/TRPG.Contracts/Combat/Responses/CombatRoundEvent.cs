using System.Text.Json.Serialization;

namespace TRPG.Contracts.Combat.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CombatHitEvent), nameof(CombatHitEvent))]
[JsonDerivedType(typeof(CombatMissEvent), nameof(CombatMissEvent))]
[JsonDerivedType(typeof(CombatBlockEvent), nameof(CombatBlockEvent))]
public abstract record CombatRoundEvent(
    Guid AttackerId,
    string AttackerName,
    string AbilityName,
    Guid TargetId,
    string TargetName
);

public sealed record CombatHitEvent(
    Guid AttackerId,
    string AttackerName,
    string AbilityName,
    Guid TargetId,
    string TargetName,
    int Damage,
    DamageType DamageType,
    bool IsCritical,
    bool Killed,
    int TargetRemainingHp,
    int TargetMaximumHp,
    IReadOnlyList<ConditionType> AppliedConditions
) : CombatRoundEvent(AttackerId, AttackerName, AbilityName, TargetId, TargetName);

public sealed record CombatMissEvent(
    Guid AttackerId,
    string AttackerName,
    string AbilityName,
    Guid TargetId,
    string TargetName
) : CombatRoundEvent(AttackerId, AttackerName, AbilityName, TargetId, TargetName);

public sealed record CombatBlockEvent(
    Guid AttackerId,
    string AttackerName,
    string AbilityName,
    Guid TargetId,
    string TargetName
) : CombatRoundEvent(AttackerId, AttackerName, AbilityName, TargetId, TargetName);
