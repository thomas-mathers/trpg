using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class BuildingFootprintCatalogTests
{
    public static TheoryData<BuildingType> BuildingTypes => new(Enum.GetValues<BuildingType>());

    [Theory]
    [MemberData(nameof(BuildingTypes))]
    public void GetMinimumArea_ReturnsAPositiveArea_ForEveryBuildingType(BuildingType buildingType)
    {
        // Act
        var area = BuildingFootprintCatalog.GetMinimumArea(buildingType);

        // Assert
        Assert.True(area > 0);
    }
}
