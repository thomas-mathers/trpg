namespace TRPG.Models;

internal class Attack : Skill {
    public List<ConditionEffect> Conditions { get; init; } = [];
    public float DamageAmount { get; init; }
    public AmountType DamageAmountType { get; init; }
    public DamageType DamageType { get; init; }
}
