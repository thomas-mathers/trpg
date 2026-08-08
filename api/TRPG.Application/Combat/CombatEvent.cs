using System.Text.Json.Serialization;
using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

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
public abstract record CombatEvent;

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
) : CombatEvent;

public sealed record Miss(
    [property: JsonIgnore] Guid AttackerId,
    string AttackerName,
    string AbilityName,
    [property: JsonIgnore] Guid TargetId,
    string TargetName
) : CombatEvent;

public sealed record Block(
    [property: JsonIgnore] Guid AttackerId,
    string AttackerName,
    string AbilityName,
    [property: JsonIgnore] Guid TargetId,
    string TargetName
) : CombatEvent;

public sealed record NoAction(string CreatureName, ConditionType Condition) : CombatEvent;

public sealed record DamageTicked(
    string CreatureName,
    string AbilityName,
    DamageType DamageType,
    int Damage,
    int RemainingHp,
    int MaximumHp,
    bool Killed
) : CombatEvent;

public record BuffModifierInfo(
    float Amount,
    AmountType AmountType,
    AttributeName Attribute,
    int RemainingTurns
);

public sealed record BuffApplied(
    string SourceName,
    string AbilityName,
    string TargetName,
    IReadOnlyList<BuffModifierInfo> AppliedModifiers
) : CombatEvent;

public sealed record Healed(
    string SourceName,
    string AbilityName,
    string TargetName,
    int Amount,
    int TargetRemainingHp,
    int TargetMaximumHp
) : CombatEvent;

public sealed record HealOverTimeApplied(
    string SourceName,
    string AbilityName,
    string TargetName,
    int AmountPerTurn,
    int Duration
) : CombatEvent;

public sealed record ConsumedPotion(
    string CreatureName,
    string ItemName,
    ResourceType Resource,
    int Amount,
    int RemainingValue,
    int MaximumValue
) : CombatEvent;

public sealed record Regenerated(
    [property: JsonIgnore] Guid CombatantId,
    string CombatantName,
    int PreviousAp,
    int CurrentAp,
    int MaximumAp,
    int PreviousMp,
    int CurrentMp,
    int MaximumMp
) : CombatEvent;

public sealed record ResourceStateUpdated(
    [property: JsonIgnore] Guid CombatantId,
    string CombatantName,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
) : CombatEvent;
