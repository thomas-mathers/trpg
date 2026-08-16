using System.ComponentModel;
using System.Text.Json.Serialization;
using TRPG.Domain.Models;

namespace TRPG.Contracts.Inventory.Responses;

public enum CombatSpeedType
{
    [Description("Increased Attack Speed")]
    IncreasedAttackSpeed,

    [Description("Faster Cast Rate")]
    FasterCastRate,

    [Description("Faster Hit Recovery")]
    FasterHitRecovery,
}

public enum LeechType
{
    Life,
    Mana,
}

public enum SpecialHitType
{
    [Description("Crushing Blow")]
    CrushingBlow,

    [Description("Deadly Strike")]
    DeadlyStrike,

    [Description("Open Wounds")]
    OpenWounds,
}

public enum ProcTrigger
{
    [Description("On Striking")]
    OnStriking,

    [Description("When Struck")]
    WhenStruck,

    [Description("On Kill")]
    OnKill,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AttributeModifierSummary), "Attribute")]
[JsonDerivedType(typeof(CombatSpeedModifierSummary), "CombatSpeed")]
[JsonDerivedType(typeof(ElementalDamageModifierSummary), "ElementalDamage")]
[JsonDerivedType(typeof(LeechModifierSummary), "Leech")]
[JsonDerivedType(typeof(SpecialHitModifierSummary), "SpecialHit")]
[JsonDerivedType(typeof(SkillBonusModifierSummary), "SkillBonus")]
[JsonDerivedType(typeof(ProcModifierSummary), "Proc")]
public abstract record ItemModifierSummary;

public sealed record AttributeModifierSummary(
    float Amount,
    AttributeName Attribute,
    AmountType AmountType
) : ItemModifierSummary;

public sealed record CombatSpeedModifierSummary(float Amount, CombatSpeedType SpeedType)
    : ItemModifierSummary;

public sealed record ElementalDamageModifierSummary(
    DamageType DamageType,
    int MinDamage,
    int MaxDamage
) : ItemModifierSummary;

public sealed record LeechModifierSummary(LeechType LeechType, float Percent) : ItemModifierSummary;

public sealed record SpecialHitModifierSummary(float Chance, SpecialHitType HitType)
    : ItemModifierSummary;

public sealed record SkillBonusModifierSummary(int Amount, Abilities.Responses.Skill? Skill)
    : ItemModifierSummary;

public sealed record ProcModifierSummary(string AbilityName, float Chance, ProcTrigger Trigger)
    : ItemModifierSummary;
