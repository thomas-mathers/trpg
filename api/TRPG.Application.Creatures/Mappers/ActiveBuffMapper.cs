using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class ActiveBuffMapper
{
    public static TRPG.Application.CreatureFormulas.ActiveBuff ToState(this ActiveBuff effect) =>
        new()
        {
            AbilityName = effect.AbilityName,
            Amount = effect.Amount,
            Attribute = Enum.Parse<AttributeName>(effect.Attribute),
            ExpiresAt = effect.ExpiresAt,
            AmountType = Enum.Parse<AmountType>(effect.AmountType),
        };

    public static CreatureBuffEffect ToEffect(this ActiveBuff buff) =>
        new(
            buff.AbilityName,
            Enum.Parse<AttributeName>(buff.Attribute),
            buff.Amount,
            Enum.Parse<AmountType>(buff.AmountType),
            buff.ExpiresAt
        );
}
