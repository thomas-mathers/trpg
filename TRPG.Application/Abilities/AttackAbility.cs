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

    // Turn Undead/Destroy Undead-style abilities: deals its ordinary (modest) damage against any
    // target, but multiplies it when the defender's CreatureType matches - the multiplier makes
    // it the AI's best choice specifically against that creature type without needing it gated
    // off or useless elsewhere. Settable (not init) so AbilityDefinitions' fluent AttackAbilityEntry
    // can populate it after construction, matching how Dots/Conditions are populated post-construction.
    public CreatureType? BonusTargetCreatureType { get; set; }
    public float BonusDamageMultiplier { get; set; } = 1f;
}
