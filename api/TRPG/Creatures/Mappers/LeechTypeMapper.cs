using ContractLeechType = TRPG.Contracts.Inventory.Responses.LeechType;
using DataLeechType = TRPG.Data.Models.LeechType;

namespace TRPG.Creatures.Mappers;

internal static class LeechTypeMapper
{
    public static ContractLeechType ToContract(this DataLeechType type) =>
        type switch
        {
            DataLeechType.Life => ContractLeechType.Life,
            DataLeechType.Mana => ContractLeechType.Mana,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
