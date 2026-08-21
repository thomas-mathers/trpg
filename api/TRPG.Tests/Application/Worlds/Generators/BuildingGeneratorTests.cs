using TRPG.Application.Worlds.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class BuildingGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void Generate_AssignsOwnerToEveryProp_WhenBuildingHasAnOwner()
    {
        // Arrange
        var ownerCreatureId = Guid.NewGuid();

        // Act
        var result = GenerateBuilding(ownerCreatureId);

        // Assert
        Assert.NotEmpty(result.Props);
        Assert.All(result.Props, prop => Assert.Equal(ownerCreatureId, prop.OwnerCreatureId));
    }

    [Fact]
    public void Generate_LeavesPropsUnowned_WhenBuildingHasNoOwner()
    {
        // Act
        var result = GenerateBuilding(ownerCreatureId: null);

        // Assert
        Assert.NotEmpty(result.Props);
        Assert.All(result.Props, prop => Assert.Null(prop.OwnerCreatureId));
    }

    private BuildingGeneratorResult GenerateBuilding(Guid? ownerCreatureId)
    {
        var exteriorLocation = new Location
        {
            WorldId = _worldId,
            StateId = Guid.NewGuid(),
            Kind = LocationKind.District,
        };
        var spec = BuildingSpecCatalog.GetSpecs(
            BuildingType.Blacksmith,
            ownerCreatureId,
            [],
            bedroomGroups: null
        );

        return new BuildingGenerator().Generate(
            new BuildingGeneratorInput(exteriorLocation, spec)
            {
                Name = "Test blacksmith",
                OwnerCreatureId = ownerCreatureId,
            }
        );
    }
}
