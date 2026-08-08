using System.Text.Json.Serialization;

namespace TRPG.Contracts.Combat.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CombatHitEvent), nameof(CombatHitEvent))]
[JsonDerivedType(typeof(CombatMissEvent), nameof(CombatMissEvent))]
[JsonDerivedType(typeof(CombatBlockEvent), nameof(CombatBlockEvent))]
[JsonDerivedType(typeof(CombatRegeneratedEvent), nameof(CombatRegeneratedEvent))]
[JsonDerivedType(typeof(CombatResourceStateUpdatedEvent), nameof(CombatResourceStateUpdatedEvent))]
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

public sealed record CombatRegeneratedEvent(
    Guid AttackerId,
    string AttackerName,
    int PreviousAp,
    int CurrentAp,
    int MaximumAp,
    int PreviousMp,
    int CurrentMp,
    int MaximumMp
) : CombatRoundEvent(AttackerId, AttackerName, "Regenerate", AttackerId, AttackerName);

public sealed record CombatResourceStateUpdatedEvent(
    Guid AttackerId,
    string AttackerName,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
) : CombatRoundEvent(AttackerId, AttackerName, "Resource state updated", AttackerId, AttackerName);
