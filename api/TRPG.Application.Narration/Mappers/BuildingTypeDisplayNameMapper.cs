using TRPG.Domain.Models;

namespace TRPG.Application.Narration.Mappers;

internal static class BuildingTypeDisplayNameMapper
{
    public static string ToDisplayName(this BuildingType type) =>
        type switch
        {
            BuildingType.ArcaneShop => "Arcane Shop",
            BuildingType.Apothecary => "Apothecary",
            BuildingType.Bakery => "Bakery",
            BuildingType.Barracks => "Barracks",
            BuildingType.Blacksmith => "Blacksmith",
            BuildingType.Carpenter => "Carpenter",
            BuildingType.Castle => "Castle",
            BuildingType.Cave => "Cave",
            BuildingType.Crypt => "Crypt",
            BuildingType.GeneralGoods => "General Goods",
            BuildingType.GuildHall => "Guild Hall",
            BuildingType.House => "House",
            BuildingType.Inn => "Inn",
            BuildingType.Jail => "Jail",
            BuildingType.Jeweler => "Jeweler",
            BuildingType.Library => "Library",
            BuildingType.Mine => "Mine",
            BuildingType.Ruins => "Ruins",
            BuildingType.Stable => "Stable",
            BuildingType.Tailor => "Tailor",
            BuildingType.Tavern => "Tavern",
            BuildingType.Temple => "Temple",
            BuildingType.Tower => "Tower",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
