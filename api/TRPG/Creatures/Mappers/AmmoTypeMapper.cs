using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class AmmoTypeMapper
{
    public static ItemType ToContract(this AmmoType type) =>
        type switch
        {
            AmmoType.Arrow => ItemType.Arrow,
            AmmoType.Bolt => ItemType.Bolt,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
