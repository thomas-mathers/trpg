using System.Text.Json.Serialization;
using Tapper;
using TypedSignalR.Client;

namespace TRPG.Combat.ClientModels;

[TranspilationSource]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CombatHitEntry), "CombatHitEvent")]
[JsonDerivedType(typeof(CombatMissEntry), "CombatMissEvent")]
[JsonDerivedType(typeof(CombatBlockEntry), "CombatBlockEvent")]
[JsonDerivedType(typeof(CombatRegeneratedEntry), "CombatRegeneratedEvent")]
[JsonDerivedType(typeof(CombatResourceStateUpdatedEntry), "CombatResourceStateUpdatedEvent")]
public abstract record CombatRoundEntry(
    Guid AttackerId,
    string AttackerName,
    string AbilityName,
    Guid TargetId,
    string TargetName
)
{
    public string? Narration { get; init; }
}

[TranspilationSource]
public sealed record CombatHitEntry(
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
) : CombatRoundEntry(AttackerId, AttackerName, AbilityName, TargetId, TargetName);

[TranspilationSource]
public sealed record CombatMissEntry(
    Guid AttackerId,
    string AttackerName,
    string AbilityName,
    Guid TargetId,
    string TargetName
) : CombatRoundEntry(AttackerId, AttackerName, AbilityName, TargetId, TargetName);

[TranspilationSource]
public sealed record CombatBlockEntry(
    Guid AttackerId,
    string AttackerName,
    string AbilityName,
    Guid TargetId,
    string TargetName
) : CombatRoundEntry(AttackerId, AttackerName, AbilityName, TargetId, TargetName);

[TranspilationSource]
public sealed record CombatRegeneratedEntry(
    Guid AttackerId,
    string AttackerName,
    int PreviousAp,
    int CurrentAp,
    int MaximumAp,
    int PreviousMp,
    int CurrentMp,
    int MaximumMp
) : CombatRoundEntry(AttackerId, AttackerName, "Regenerate", AttackerId, AttackerName);

[TranspilationSource]
public sealed record CombatResourceStateUpdatedEntry(
    Guid AttackerId,
    string AttackerName,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
) : CombatRoundEntry(AttackerId, AttackerName, "Resource state updated", AttackerId, AttackerName);
