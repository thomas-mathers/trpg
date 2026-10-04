using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class BuildingFootprintCatalog
{
    private static readonly Dictionary<BuildingType, double> MinimumAreaByBuildingType = new()
    {
        [BuildingType.Cave] = 30,
        [BuildingType.Crypt] = 30,
        [BuildingType.Mine] = 30,
        [BuildingType.Ruins] = 40,
        [BuildingType.Tower] = 30,
    };

    internal static double GetMinimumArea(BuildingType buildingType) =>
        MinimumAreaByBuildingType[buildingType];
}
