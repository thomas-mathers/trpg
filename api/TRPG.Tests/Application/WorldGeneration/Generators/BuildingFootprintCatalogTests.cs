using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class BuildingFootprintCatalogTests
{
    public static TheoryData<BuildingType> DungeonTypes => new(BuildingTypes.Dungeon);

    [Theory]
    [MemberData(nameof(DungeonTypes))]
    public void GetMinimumArea_ReturnsAPositiveArea_ForEveryDungeonType(BuildingType buildingType)
    {
        // Act
        var area = BuildingFootprintCatalog.GetMinimumArea(buildingType);

        // Assert
        Assert.True(area > 0);
    }
}
