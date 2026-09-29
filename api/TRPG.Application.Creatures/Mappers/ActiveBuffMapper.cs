using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class ActiveBuffMapper
{
    public static CreatureBuffEffect ToEffect(this ActiveBuff buff) =>
        new(
            buff.AbilityName,
            Enum.Parse<AttributeName>(buff.Attribute),
            buff.Amount,
            Enum.Parse<AmountType>(buff.AmountType),
            buff.ExpiresAt
        );
}
