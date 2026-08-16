using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class AccessoryTypeMapper
{
    public static ItemType ToContract(this AccessoryType type) =>
        type switch
        {
            AccessoryType.Ring => ItemType.Ring,
            AccessoryType.Necklace => ItemType.Necklace,
            AccessoryType.Belt => ItemType.Belt,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
