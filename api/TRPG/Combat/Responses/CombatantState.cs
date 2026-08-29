using System.ComponentModel;
using Tapper;
using TypedSignalR.Client;

namespace TRPG.Combat.Responses;

[TranspilationSource]
public enum DamageType
{
    Physical,
    Fire,
    Ice,
    Lightning,
    Poison,
    Magic,
}

[TranspilationSource]
public enum AttributeName
{
    [Description("Maximum HP")]
    MaximumHp,

    [Description("Maximum AP")]
    MaximumAp,

    [Description("Maximum MP")]
    MaximumMp,

    [Description("Carrying Capacity")]
    CarryingCapacity,
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

[TranspilationSource]
public enum AmountType
{
    Flat,
    Percent,
}

[TranspilationSource]
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

[TranspilationSource]
public enum CombatOutcome
{
    Ongoing,
    Victory,
    Defeat,
    Fled,
}

[TranspilationSource]
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
    ActiveConditions ActiveConditions,
    IReadOnlyCollection<ActiveDot> ActiveDots,
    IReadOnlyCollection<ActiveHot> ActiveHots,
    IReadOnlyCollection<ActiveBuff> ActiveBuffs
);

[TranspilationSource]
public record ActiveDot(string AbilityName, int Amount, DamageType DamageType, int RemainingTurns);

[TranspilationSource]
public record ActiveHot(string AbilityName, int Amount, int RemainingTurns);

[TranspilationSource]
public record ActiveBuff(
    string AbilityName,
    AttributeName Attribute,
    float Amount,
    AmountType AmountType,
    int RemainingTurns
);
