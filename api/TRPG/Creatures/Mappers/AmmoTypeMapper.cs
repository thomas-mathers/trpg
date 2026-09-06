using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class AmmoTypeMapper
{
    public static ItemType ToResponse(this AmmoType type) =>
        type switch
        {
            AmmoType.Arrow => ItemType.Arrow,
            AmmoType.Bolt => ItemType.Bolt,
        };
}
