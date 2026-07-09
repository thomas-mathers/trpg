using System.Text.Json.Serialization;

namespace TRPG.Data.Models;

public enum CombatSpeedType
{
    IncreasedAttackSpeed,
    FasterCastRate,
    FasterHitRecovery,
}

public enum LeechType
{
    Life,
    Mana,
}

public enum SpecialHitType
{
    CrushingBlow,
    DeadlyStrike,
    OpenWounds,
}

public enum ProcTrigger
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
public abstract class ItemModifier
{
    public int MinItemLevel { get; init; }
}

public class CombatSpeedModifier : ItemModifier
{
    public float Amount { get; init; }
    public CombatSpeedType SpeedType { get; init; }
}

public class ElementalDamageModifier : ItemModifier
{
    public DamageType DamageType { get; init; }
    public int MaxDamage { get; init; }
    public int MinDamage { get; init; }
}

public class LeechModifier : ItemModifier
{
    public LeechType LeechType { get; init; }
    public float Percent { get; init; }
}

public class SpecialHitModifier : ItemModifier
{
    public float Chance { get; init; }
    public SpecialHitType HitType { get; init; }
}

public class SkillBonusModifier : ItemModifier
{
    public int Amount { get; init; }
    public Skill? Skill { get; init; }
}

public class ProcModifier : ItemModifier
{
    public string AbilityName { get; init; } = "";
    public float Chance { get; init; }
    public ProcTrigger Trigger { get; init; }
}
