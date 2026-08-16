using ContractAmountType = TRPG.Application.Combat.Responses.AmountType;
using DataAmountType = TRPG.Domain.Models.AmountType;

namespace TRPG.Application.Combat.Mappers;

internal static class AmountTypeMapper
{
    public static ContractAmountType ToContract(this DataAmountType type) =>
        type switch
        {
            DataAmountType.Flat => ContractAmountType.Flat,
            DataAmountType.Percent => ContractAmountType.Percent,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
