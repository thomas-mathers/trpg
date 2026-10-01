using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class BuildingFootprintCatalog
{
    private static readonly Dictionary<BuildingType, double> MinimumAreaByBuildingType = new()
    {
        [BuildingType.ArcaneShop] = 30,
        [BuildingType.Apothecary] = 30,
        [BuildingType.Bakery] = 30,
        [BuildingType.Barracks] = 120,
        [BuildingType.Blacksmith] = 60,
        [BuildingType.Carpenter] = 50,
        [BuildingType.Castle] = 400,
        [BuildingType.Cave] = 30,
        [BuildingType.Crypt] = 30,
        [BuildingType.GeneralGoods] = 40,
        [BuildingType.GuildHall] = 150,
        [BuildingType.House] = 25,
        [BuildingType.Inn] = 100,
        [BuildingType.Jail] = 80,
        [BuildingType.Jeweler] = 25,
        [BuildingType.Library] = 100,
        [BuildingType.Mine] = 30,
        [BuildingType.Ruins] = 40,
        [BuildingType.Stable] = 80,
        [BuildingType.Tailor] = 30,
        [BuildingType.Tavern] = 80,
        [BuildingType.Temple] = 200,
        [BuildingType.Tower] = 30,
    };

    internal static double GetMinimumArea(BuildingType buildingType) =>
        MinimumAreaByBuildingType[buildingType];
}
