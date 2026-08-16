using System.Collections.Frozen;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings;

public static class ShopBuildingTypes
{
    private static readonly FrozenSet<BuildingType> Types = new BuildingType[]
    {
        BuildingType.ArcaneShop,
        BuildingType.Apothecary,
        BuildingType.Bakery,
        BuildingType.Barracks,
        BuildingType.Blacksmith,
        BuildingType.Carpenter,
        BuildingType.Castle,
        BuildingType.GeneralGoods,
        BuildingType.GuildHall,
        BuildingType.Inn,
        BuildingType.Jail,
        BuildingType.Jeweler,
        BuildingType.Library,
        BuildingType.Stable,
        BuildingType.Tailor,
        BuildingType.Tavern,
        BuildingType.Temple,
    }.ToFrozenSet();

    public static IReadOnlyCollection<BuildingType> All => Types;

    public static bool IsShop(BuildingType buildingType) => Types.Contains(buildingType);
}
