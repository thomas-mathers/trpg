using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class ArmorTypeMapper
{
    public static ItemType ToResponse(this ArmorType type) =>
        type switch
        {
            ArmorType.Helm => ItemType.Helm,
            ArmorType.Chest => ItemType.Chest,
            ArmorType.Boots => ItemType.Boots,
            ArmorType.Gloves => ItemType.Gloves,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
