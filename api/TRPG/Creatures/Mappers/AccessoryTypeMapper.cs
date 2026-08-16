using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class AccessoryTypeMapper
{
    public static ItemType ToResponse(this AccessoryType type) =>
        type switch
        {
            AccessoryType.Ring => ItemType.Ring,
            AccessoryType.Necklace => ItemType.Necklace,
            AccessoryType.Belt => ItemType.Belt,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
