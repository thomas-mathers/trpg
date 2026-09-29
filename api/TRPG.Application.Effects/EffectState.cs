using TRPG.Application.Abilities;
using TRPG.Application.CreatureFormulas;
using TRPG.Domain;
using TRPG.Domain.Models;
using ActiveBuff = TRPG.Application.CreatureFormulas.ActiveBuff;

namespace TRPG.Application.Effects;

public sealed class EffectState
{
    public required Attributes Attributes { get; init; }
    public IReadOnlyList<Item> EquippedItems { get; init; } = [];
    public int CurrentHp { get; set; }
    public Dictionary<ConditionType, GameInstant> ActiveConditions { get; init; } = [];
    public Dictionary<string, GameInstant> CooldownReadyAtByAbility { get; init; } = [];
    public List<ActiveDot> ActiveDots { get; init; } = [];
    public List<ActiveHot> ActiveHots { get; init; } = [];
    public List<ActiveBuff> ActiveBuffs { get; init; } = [];
    public bool IsAlive => CurrentHp > 0;
    public int MaximumHp => (int)CalculateAttribute(AttributeName.MaximumHp);

    public float ResistanceFor(DamageType damageType) =>
        CalculateAttribute(DamageMitigation.ResistanceAttribute(damageType));

    private float CalculateAttribute(AttributeName attribute) =>
        StatFormulas.CalculateEffectiveAttribute(Attributes, ActiveBuffs, EquippedItems, attribute);
}

public sealed class ActiveDot
{
    public string AbilityName { get; init; } = "";
    public int Amount { get; init; }
    public DamageType DamageType { get; init; }
    public GameInstant NextTickAt { get; set; }
    public GameInstant ExpiresAt { get; init; }
}

public sealed class ActiveHot
{
    public string AbilityName { get; init; } = "";
    public int Amount { get; init; }
    public GameInstant NextTickAt { get; set; }
    public GameInstant ExpiresAt { get; init; }
}
