using TRPG.Application.Abilities;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Results;

public record CreatureEffects(
    IReadOnlyDictionary<ConditionType, GameInstant> Conditions,
    IReadOnlyCollection<CreatureDotEffect> Dots,
    IReadOnlyCollection<CreatureHotEffect> Hots,
    IReadOnlyCollection<CreatureBuffEffect> Buffs
)
{
    public static CreatureEffects None { get; } =
        new(new Dictionary<ConditionType, GameInstant>(), [], [], []);
}

public record CreatureDotEffect(
    string AbilityName,
    int Amount,
    DamageType DamageType,
    GameInstant ExpiresAt
);

public record CreatureHotEffect(string AbilityName, int Amount, GameInstant ExpiresAt);

public record CreatureBuffEffect(
    string AbilityName,
    AttributeName Attribute,
    float Amount,
    AmountType AmountType,
    GameInstant ExpiresAt
);
