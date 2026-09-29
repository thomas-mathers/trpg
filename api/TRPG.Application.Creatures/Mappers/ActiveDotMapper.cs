using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class ActiveDotMapper
{
    public static TRPG.Application.Effects.ActiveDot ToState(this ActiveDot effect) =>
        new()
        {
            AbilityName = effect.AbilityName,
            Amount = effect.Amount,
            DamageType = Enum.Parse<DamageType>(effect.DamageType),
            NextTickAt = effect.NextTickAt,
            ExpiresAt = effect.ExpiresAt,
        };

    public static CreatureDotEffect ToEffect(this ActiveDot dot) =>
        new(dot.AbilityName, dot.Amount, Enum.Parse<DamageType>(dot.DamageType), dot.ExpiresAt);
}
