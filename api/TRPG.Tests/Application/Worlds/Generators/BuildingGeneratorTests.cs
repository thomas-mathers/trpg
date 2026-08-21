using TRPG.Application.Worlds.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class BuildingGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void Generate_AssignsOwnerToEveryContainer_WhenBuildingHasAnOwner()
    {
        // Arrange
        var ownerCreatureId = Guid.NewGuid();

        // Act
        var result = GenerateBuilding(ownerCreatureId);
        var containers = result.Props.OfType<Container>().ToArray();

        // Assert
        Assert.NotEmpty(containers);
        Assert.All(
            containers,
            container => Assert.Equal(ownerCreatureId, container.OwnerCreatureId)
        );
    }

    [Fact]
    public void Generate_LeavesContainersUnowned_WhenBuildingHasNoOwner()
    {
        // Act
        var result = GenerateBuilding(ownerCreatureId: null);
        var containers = result.Props.OfType<Container>().ToArray();

        // Assert
        Assert.NotEmpty(containers);
        Assert.All(containers, container => Assert.Null(container.OwnerCreatureId));
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
