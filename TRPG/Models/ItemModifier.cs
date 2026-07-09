using System.Text.Json.Serialization;

namespace TRPG.Models;

internal enum CombatSpeedType
{
    IncreasedAttackSpeed,
    FasterCastRate,
    FasterHitRecovery,
}

internal enum LeechType
{
    Life,
    Mana,
}

internal enum SpecialHitType
{
    CrushingBlow,
    DeadlyStrike,
    OpenWounds,
}

internal enum ProcTrigger
{
    OnStriking,
    WhenStruck,
    OnKill,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AttributeModifier), "attribute")]
[JsonDerivedType(typeof(CombatSpeedModifier), "combat_speed")]
[JsonDerivedType(typeof(ElementalDamageModifier), "elemental_damage")]
[JsonDerivedType(typeof(LeechModifier), "leech")]
[JsonDerivedType(typeof(SpecialHitModifier), "special_hit")]
[JsonDerivedType(typeof(SkillBonusModifier), "skill_bonus")]
[JsonDerivedType(typeof(ProcModifier), "proc")]
internal abstract class ItemModifier
{
    public int MinItemLevel { get; init; }
}

internal class CombatSpeedModifier : ItemModifier
{
    public float Amount { get; init; }
    public CombatSpeedType SpeedType { get; init; }
}

internal class ElementalDamageModifier : ItemModifier
{
    public DamageType DamageType { get; init; }
    public int MaxDamage { get; init; }
    public int MinDamage { get; init; }
}

internal class LeechModifier : ItemModifier
{
    public LeechType LeechType { get; init; }
    public float Percent { get; init; }
}

internal class SpecialHitModifier : ItemModifier
{
    public float Chance { get; init; }
    public SpecialHitType HitType { get; init; }
}

internal class SkillBonusModifier : ItemModifier
{
    public int Amount { get; init; }
    public Skill? Skill { get; init; }
}

internal class ProcModifier : ItemModifier
{
    public string AbilityName { get; init; } = "";
    public float Chance { get; init; }
    public ProcTrigger Trigger { get; init; }
}
