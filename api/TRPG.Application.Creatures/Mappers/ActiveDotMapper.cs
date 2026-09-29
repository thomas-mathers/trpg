using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class ActiveDotMapper
{
    public static CreatureDotEffect ToEffect(this ActiveDot dot) =>
        new(dot.AbilityName, dot.Amount, Enum.Parse<DamageType>(dot.DamageType), dot.ExpiresAt);
}
