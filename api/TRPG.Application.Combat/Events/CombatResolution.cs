using System.Text.Json.Serialization;
using TRPG.Application.Abilities;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Events;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Hit), "Hit")]
[JsonDerivedType(typeof(Miss), "Miss")]
[JsonDerivedType(typeof(Block), "Block")]
[JsonDerivedType(typeof(NoAction), "NoAction")]
[JsonDerivedType(typeof(DamageTicked), "DamageTicked")]
[JsonDerivedType(typeof(BuffApplied), "BuffApplied")]
[JsonDerivedType(typeof(Healed), "Healed")]
[JsonDerivedType(typeof(HealOverTimeApplied), "HealOverTimeApplied")]
[JsonDerivedType(typeof(ConsumedPotion), "ConsumedPotion")]
[JsonDerivedType(typeof(Regenerated), "Regenerated")]
[JsonDerivedType(typeof(ResourceStateUpdated), "ResourceStateUpdated")]
[JsonDerivedType(typeof(FleeFailed), "FleeFailed")]
public abstract record CombatResolution;

public sealed record Hit(
    [property: JsonIgnore] Guid AttackerId,
    string AttackerName,
    string AbilityName,
    [property: JsonIgnore] Guid TargetId,
    string TargetName,
    int TargetRemainingHp,
    int TargetMaximumHp,
    bool Killed,
    bool IsCritical,
    int Damage,
    DamageType DamageType,
    IReadOnlyList<ConditionType> AppliedConditions
) : CombatResolution;

public sealed record Miss(
    [property: JsonIgnore] Guid AttackerId,
    string AttackerName,
    string AbilityName,
    [property: JsonIgnore] Guid TargetId,
    string TargetName
) : CombatResolution;

public sealed record Block(
    [property: JsonIgnore] Guid AttackerId,
    string AttackerName,
    string AbilityName,
    [property: JsonIgnore] Guid TargetId,
    string TargetName
) : CombatResolution;

public sealed record NoAction(string CreatureName, ConditionType Condition) : CombatResolution;

public sealed record DamageTicked(
    string CreatureName,
    string AbilityName,
    DamageType DamageType,
    int Damage,
    int RemainingHp,
    int MaximumHp,
    bool Killed
) : CombatResolution;

public record BuffModifierInfo(
    float Amount,
    AmountType AmountType,
    AttributeName Attribute,
    int RemainingTurns
);

public sealed record BuffApplied(
    [property: JsonIgnore] Guid SourceId,
    string SourceName,
    string AbilityName,
    [property: JsonIgnore] Guid TargetId,
    string TargetName,
    IReadOnlyList<BuffModifierInfo> AppliedModifiers
) : CombatResolution;

public sealed record Healed(
    [property: JsonIgnore] Guid SourceId,
    string SourceName,
    string AbilityName,
    [property: JsonIgnore] Guid TargetId,
    string TargetName,
    int Amount,
    int TargetRemainingHp,
    int TargetMaximumHp
) : CombatResolution;

public sealed record HealOverTimeApplied(
    [property: JsonIgnore] Guid SourceId,
    string SourceName,
    string AbilityName,
    [property: JsonIgnore] Guid TargetId,
    string TargetName,
    int AmountPerTurn,
    int Duration
) : CombatResolution;

public sealed record ConsumedPotion(
    [property: JsonIgnore] Guid CreatureId,
    string CreatureName,
    string ItemName,
    ResourceType Resource,
    int Amount,
    int RemainingValue,
    int MaximumValue
) : CombatResolution;

public sealed record Regenerated(
    [property: JsonIgnore] Guid CombatantId,
    string CombatantName,
    int PreviousAp,
    int CurrentAp,
    int MaximumAp,
    int PreviousMp,
    int CurrentMp,
    int MaximumMp
) : CombatResolution;

public sealed record ResourceStateUpdated(
    [property: JsonIgnore] Guid CombatantId,
    string CombatantName,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
) : CombatResolution;

public sealed record FleeFailed([property: JsonIgnore] Guid CreatureId, string CreatureName)
    : CombatResolution;
