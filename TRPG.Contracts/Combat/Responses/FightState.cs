using System.ComponentModel;

namespace TRPG.Contracts.Combat.Responses;

public enum DamageType
{
    Physical,
    Fire,
    Ice,
    Lightning,
    Poison,
    Magic,
}

public enum AttributeName
{
    [Description("Maximum HP")]
    MaximumHp,

    [Description("Maximum AP")]
    MaximumAp,

    [Description("Maximum MP")]
    MaximumMp,
    Strength,
    Defense,
    Dexterity,
    Endurance,
    Stamina,
    Mana,
    Intelligence,

    [Description("Physical Resistance")]
    PhysicalResistance,

    [Description("Fire Resistance")]
    FireResistance,

    [Description("Ice Resistance")]
    IceResistance,

    [Description("Lightning Resistance")]
    LightningResistance,

    [Description("Poison Resistance")]
    PoisonResistance,

    [Description("Magic Resistance")]
    MagicResistance,

    [Description("Movement Speed")]
    MovementSpeed,
}

public enum AmountType
{
    Flat,
    Percent,
}

public enum ConditionType
{
    Blinded,
    Bleeding,
    Burning,
    Disarmed,
    Frozen,
    Poisoned,
    Silenced,
    Snared,
    Stunned,
}

public record FightState(IReadOnlyCollection<CombatantState> Combatants);

public record CombatantState(
    Guid Id,
    string Name,
    int Level,
    bool IsPlayer,
    bool IsAlive,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    IReadOnlyDictionary<ConditionType, int> ActiveConditions,
    IReadOnlyCollection<ActiveDot> ActiveDots,
    IReadOnlyCollection<ActiveHot> ActiveHots,
    IReadOnlyCollection<ActiveBuff> ActiveBuffs
);

public record ActiveDot(string AbilityName, int Amount, DamageType DamageType, int RemainingTurns);

public record ActiveHot(string AbilityName, int Amount, int RemainingTurns);

public record ActiveBuff(
    string AbilityName,
    AttributeName Attribute,
    float Amount,
    AmountType AmountType,
    int RemainingTurns
);
