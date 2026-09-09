using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

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
    public void Generate_ReturnsABuildingOfConnectedRooms()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        Assert.Equal(_wildernessLocationId, result.Building.ExteriorLocationId);
        Assert.True(result.Rooms.Count > 1);
        Assert.All(result.Rooms, room => Assert.Equal(result.Building.Id, room.BuildingId));
        Assert.All(result.Locations, location => Assert.Null(location.CityId));
    }

    [Fact]
    public void Generate_GivesEveryRoomAPosition_SoTheDungeonCanBeMapped()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        Assert.All(result.Rooms, room => Assert.NotNull(room.Position));
    }

    [Fact]
    public void Generate_NamesEveryRoomDistinctly_SoAnExitCanBeAskedForByName()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        var names = result.Rooms.Select(room => room.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Generate_LeavesEveryPassageWalkableBothWays()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert — a room you can walk into but not out of is a trap, not a dungeon.
        var interior = result
            .LocationConnectors.Where(connector =>
                connector.OriginLocationId != _wildernessLocationId
                && connector.DestinationLocationId != _wildernessLocationId
            )
            .ToArray();
        Assert.All(
            interior,
            connector =>
                Assert.Contains(
                    interior,
                    other =>
                        other.OriginLocationId == connector.DestinationLocationId
                        && other.DestinationLocationId == connector.OriginLocationId
                )
        );
    }

    [Fact]
    public void Generate_ReturnsAFrontDoorConnector_LeadingToTheWilderness()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        var frontDoor = result.LocationConnectors.Single(connector =>
            connector.DestinationLocationId == _wildernessLocationId
        );
        Assert.Equal(result.EntranceLocationId, frontDoor.OriginLocationId);
        Assert.Equal(frontDoor.Id, result.Door.ConnectorId);
    }

    [Fact]
    public void Generate_PutsTheBossSomewhereOtherThanTheEntrance()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        Assert.NotEqual(result.EntranceLocationId, result.BossLocationId);
    }

    [Fact]
    public void Generate_NeverPicksAnExcludedName()
    {
        // Arrange
        var result = DungeonGenerator.Generate(MakeInput());

        // Act
        var next = DungeonGenerator.Generate(MakeInput([result.Building.Name]));

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
            var result = DungeonGenerator.Generate(MakeInput(usedNames));
            usedNames.Add(result.Building.Name);
        }

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            DungeonGenerator.Generate(MakeInput(usedNames))
        );
    }

    private DungeonGeneratorInput MakeInput(IReadOnlyCollection<string>? excludedNames = null) =>
        new(excludedNames ?? [], WildernessLocation, _worldId) { Random = new Random(20260908) };
}
