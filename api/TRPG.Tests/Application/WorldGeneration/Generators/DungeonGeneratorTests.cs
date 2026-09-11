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
    public void Generate_GivesEveryRoomBounds_SoTheDungeonCanBeMapped()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        Assert.All(result.Rooms, room => Assert.NotNull(room.Bounds));
    }

    [Fact]
    public void Generate_ScalesRoomsByTheirRole()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        var entrance = result.Rooms.Single(room => room.Role == RoomRole.Entrance).Bounds!;
        var boss = result.Rooms.Single(room => room.Role == RoomRole.BossChamber).Bounds!;
        Assert.True(boss.Right - boss.Left > entrance.Right - entrance.Left);
        Assert.True(boss.Bottom - boss.Top > entrance.Bottom - entrance.Top);
    }

    [Fact]
    public void Generate_EndsEveryMappedPassage_OnItsRoomWalls()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        var roomsByLocation = result.Rooms.ToDictionary(room => room.LocationId);
        var mappedPassages = result.LocationConnectors.Where(connector => connector.Path != null);
        Assert.All(
            mappedPassages,
            connector =>
            {
                AssertOnWall(
                    roomsByLocation[connector.OriginLocationId].Bounds!,
                    connector.Path!.Points[0]
                );
                AssertOnWall(
                    roomsByLocation[connector.DestinationLocationId].Bounds!,
                    connector.Path.Points[^1]
                );
            }
        );
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
    public void Generate_ReturnsABackDoorLocation_ConnectedToBothTheEntranceAndTheBoss()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert — every dungeon gets a mandatory shortcut back near the entrance once the boss is
        // reached, distinct from both endpoints.
        Assert.NotEqual(result.EntranceLocationId, result.BackDoorLocationId);
        Assert.NotEqual(result.BossLocationId, result.BackDoorLocationId);
        Assert.Contains(
            result.LocationConnectors,
            connector =>
                connector.OriginLocationId == result.EntranceLocationId
                && connector.DestinationLocationId == result.BackDoorLocationId
        );
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

    [Fact]
    public void Generate_DescribesRoomsDistinctly_SoTheyAreWorthTellingApart()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert — a role has only a few base lines, so the detail is what stops them repeating.
        var descriptions = result.Rooms.Select(room => room.Description).ToArray();
        Assert.True(descriptions.Distinct().Count() > descriptions.Length / 2);
    }

    [Fact]
    public void Generate_GivesAFewRoomsSomethingSingularToSteerBy()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert — every room having a landmark would mean none of them did.
        var longest = result.Rooms.OrderByDescending(room => room.Description.Length).ToArray();
        Assert.True(longest.Length > 4);
        Assert.True(longest[0].Description.Length > longest[^1].Description.Length);
    }

    [Fact]
    public void Generate_RecordsWhatEachRoomIsFor()
    {
        // Act
        var result = DungeonGenerator.Generate(MakeInput());

        // Assert
        Assert.All(result.Rooms, room => Assert.NotNull(room.Role));
        Assert.Contains(result.Rooms, room => room.Role == RoomRole.BossChamber);
        Assert.Contains(result.Rooms, room => room.Role == RoomRole.Entrance);
    }

    private DungeonGeneratorInput MakeInput(IReadOnlyCollection<string>? excludedNames = null) =>
        new(excludedNames ?? [], WildernessLocation, _worldId) { Random = new Random(20260908) };

    private static void AssertOnWall(Rectangle bounds, Point point) =>
        Assert.True(
            point.X == bounds.Left
                || point.X == bounds.Right
                || point.Y == bounds.Top
                || point.Y == bounds.Bottom
        );
}
