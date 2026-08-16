using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class ArmorTypeMapper
{
    public static ItemType ToContract(this ArmorType type) =>
        type switch
        {
            ArmorType.Helm => ItemType.Helm,
            ArmorType.Chest => ItemType.Chest,
            ArmorType.Boots => ItemType.Boots,
            ArmorType.Gloves => ItemType.Gloves,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
