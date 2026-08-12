using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class DungeonGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _wildernessLocationId = Guid.NewGuid();

    private Location WildernessLocation =>
        new()
        {
            Id = _wildernessLocationId,
            StateId = _stateId,
            WorldId = _worldId,
            Kind = LocationKind.Wilderness,
        };

    [Fact]
    public void Generate_ReturnsBuildingAndGroundFloorRoom()
    {
        // Act
        var result = DungeonGenerator.Generate(
            new DungeonGeneratorInput([], WildernessLocation, _worldId)
        );

        // Assert
        Assert.Equal(_wildernessLocationId, result.Building.ExteriorLocationId);
        Assert.Equal(_worldId, result.Building.WorldId);
        Assert.Equal(result.Building.Id, result.Room.BuildingId);
        Assert.Equal(0, result.Room.FloorNumber);
        Assert.Equal(result.Location.Id, result.Room.LocationId);
        Assert.Null(result.Location.CityId);
        Assert.Null(result.Location.DistrictId);
    }

    [Fact]
    public void Generate_ReturnsAFrontDoorConnector_LeadingToTheWilderness()
    {
        // Act
        var result = DungeonGenerator.Generate(
            new DungeonGeneratorInput([], WildernessLocation, _worldId)
        );

        // Assert
        var connector = Assert.IsType<LocationConnector>(Assert.Single(result.Props));
        Assert.Equal(result.Room.LocationId, connector.LocationId);
        Assert.Equal(_wildernessLocationId, connector.DestinationLocationId);
    }

    [Fact]
    public void Generate_NeverPicksAnExcludedName()
    {
        // Arrange
        var result = DungeonGenerator.Generate(
            new DungeonGeneratorInput([], WildernessLocation, _worldId)
        );

        // Act
        var next = DungeonGenerator.Generate(
            new DungeonGeneratorInput([result.Building.Name], WildernessLocation, _worldId)
        );

        // Assert
        Assert.NotEqual(result.Building.Name, next.Building.Name);
    }

    [Fact]
    public void Generate_Throws_WhenNamePoolIsExhausted()
    {
        // Arrange
        var usedNames = new HashSet<string>();
        for (var i = 0; i < DungeonGenerator.TotalNameCount; i++)
        {
            var result = DungeonGenerator.Generate(
                new DungeonGeneratorInput(usedNames, WildernessLocation, _worldId)
            );
            usedNames.Add(result.Building.Name);
        }

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            DungeonGenerator.Generate(
                new DungeonGeneratorInput(usedNames, WildernessLocation, _worldId)
            )
        );
    }
}
