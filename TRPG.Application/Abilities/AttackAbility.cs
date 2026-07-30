using TRPG.Data.Models;

namespace TRPG.Application.Abilities;

public enum AttackTargetType
{
    Single,
    Aoe,
}

public class AttackAbility : Ability
{
    public AttackTargetType TargetType { get; init; }
    public List<DotEffect> Dots { get; init; } = [];
    public List<StatusEffect> Conditions { get; init; } = [];
    public float DamageAmount { get; init; }
    public AmountType DamageAmountType { get; init; }
    public DamageType DamageType { get; init; }

    // Multiplies damage when the defender's CreatureType matches (e.g. Turn Undead vs Undead).
    public CreatureType? BonusTargetCreatureType { get; set; }
    public float BonusDamageMultiplier { get; set; } = 1f;
}
