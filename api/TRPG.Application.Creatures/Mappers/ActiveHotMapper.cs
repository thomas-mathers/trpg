using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class ActiveHotMapper
{
    public static TRPG.Application.Effects.ActiveHot ToState(this ActiveHot effect) =>
        new()
        {
            AbilityName = effect.AbilityName,
            Amount = effect.Amount,
            NextTickAt = effect.NextTickAt,
            ExpiresAt = effect.ExpiresAt,
        };

    public static CreatureHotEffect ToEffect(this ActiveHot hot) =>
        new(hot.AbilityName, hot.Amount, hot.ExpiresAt);
}
