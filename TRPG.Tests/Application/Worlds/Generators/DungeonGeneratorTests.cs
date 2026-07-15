using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class DungeonGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();

    [Fact]
    public void Generate_ReturnsBuildingAndGroundFloorRoom()
    {
        // Act
        var result = DungeonGenerator.Generate(
            new DungeonGeneratorInput(_stateId, [], _worldId)
        );

        // Assert
        Assert.Equal(_stateId, result.Building.StateId);
        Assert.Equal(_worldId, result.Building.WorldId);
        Assert.Equal(result.Building.Id, result.Room.BuildingId);
        Assert.Equal(0, result.Room.FloorNumber);
    }

    [Fact]
    public void Generate_ReturnsAFrontDoorConnector_LeadingOutside()
    {
        // Act
        var result = DungeonGenerator.Generate(new DungeonGeneratorInput(_stateId, [], _worldId));

        // Assert
        var connector = Assert.IsType<RoomConnector>(Assert.Single(result.Props));
        Assert.Equal(result.Room.Id, connector.RoomId);
        Assert.Null(connector.DestinationRoomId);
    }

    [Fact]
    public void Generate_NeverPicksAnExcludedName()
    {
        // Act
        var result = DungeonGenerator.Generate(
            new DungeonGeneratorInput(_stateId, [], _worldId)
        );
        var next = DungeonGenerator.Generate(
            new DungeonGeneratorInput(_stateId, [result.Building.Name], _worldId)
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
                new DungeonGeneratorInput(_stateId, usedNames, _worldId)
            );
            usedNames.Add(result.Building.Name);
        }

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            DungeonGenerator.Generate(new DungeonGeneratorInput(_stateId, usedNames, _worldId))
        );
    }
}
