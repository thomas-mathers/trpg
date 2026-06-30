namespace TRPG.Models;

internal enum DamageType {
    Physical,
    Fire,
    Ice,
    Lightning,
    Poison,
    Magic
}

internal class AttackAbility : Ability {
    public List<ConditionEffect> Conditions { get; init; } = [];
    public float DamageAmount { get; init; }
    public AmountType DamageAmountType { get; init; }
    public DamageType DamageType { get; init; }
}