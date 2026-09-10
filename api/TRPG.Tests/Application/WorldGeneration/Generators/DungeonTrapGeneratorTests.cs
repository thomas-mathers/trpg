using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonTrapGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();

    [Fact]
    public void Generate_NeverExceedsMaxTrapsPerDungeon()
    {
        var generator = MakeGenerator(maxTrapsPerDungeon: 2);
        var placements = MakePlacements(
            (RoomRole.TreasureRoom, 3),
            (RoomRole.CollapsedGallery, 4),
            (RoomRole.Passage, 5),
            (RoomRole.FloodedSump, 1)
        );

        for (var i = 0; i < 20; i++)
        {
            // Act
            var result = generator.Generate(placements, _worldId, _stateId, Random.Shared);

            // Assert
            Assert.True(result.Triggers.Count <= 2);
        }
    }

    [Theory]
    [InlineData(RoomRole.TreasureRoom, TrapKind.Mechanical)]
    [InlineData(RoomRole.CollapsedGallery, TrapKind.Collapse)]
    [InlineData(RoomRole.Passage, TrapKind.Slope)]
    [InlineData(RoomRole.FloodedSump, TrapKind.Water)]
    public void Generate_PicksTrapKind_FromTheRoomsRole(RoomRole role, TrapKind expectedKind)
    {
        // Water is the only kind that still needs an existing shallower room to target.
        var generator = MakeGenerator(maxTrapsPerDungeon: 1);
        var trapRoom = MakeRoom(depth: 3);
        var placements = new[]
        {
            new DungeonRoomPlacement(
                trapRoom,
                role,
                3,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ),
            new DungeonRoomPlacement(
                MakeRoom(depth: 1),
                RoomRole.Storeroom,
                1,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ),
        };

        // Act
        var trap = Assert.Single(
            generator.Generate(placements, _worldId, _stateId, Random.Shared).Triggers
        );

        // Assert
        Assert.Equal(expectedKind, trap.TrapKind);
        Assert.Equal(trapRoom.LocationId, trap.LocationId);
    }

    [Fact]
    public void Generate_CollapseAndSlopeShareOneRubbleLanding_WithAClimbBackConnector()
    {
        // Arrange
        var generator = MakeGenerator(maxTrapsPerDungeon: 2);
        var collapseRoom = MakeRoom(depth: 2);
        var slopeRoom = MakeRoom(depth: 3);
        var placements = new[]
        {
            new DungeonRoomPlacement(
                collapseRoom,
                RoomRole.CollapsedGallery,
                2,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ),
            new DungeonRoomPlacement(
                slopeRoom,
                RoomRole.Passage,
                3,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ),
        };

        // Act
        var result = generator.Generate(placements, _worldId, _stateId, Random.Shared);

        // Assert
        Assert.Equal(2, result.Triggers.Count);
        var targets = result.Triggers.Select(trap => trap.TargetId).Distinct().ToArray();
        var rubbleLanding = Assert.Single(targets);
        var rubbleLandingRoom = Assert.Single(result.Rooms);
        Assert.Equal(rubbleLandingRoom.LocationId, rubbleLanding);
        Assert.Equal(-1, rubbleLandingRoom.FloorNumber);
        Assert.Contains(
            result.LocationConnectors,
            connector => connector.OriginLocationId == rubbleLandingRoom.LocationId
        );
    }

    [Fact]
    public void Generate_MechanicalAloneStillGetsARubbleLanding_SoItCanNeverSoftLock()
    {
        // Arrange — no Collapse or Slope trap exists in this dungeon at all.
        var generator = MakeGenerator(maxTrapsPerDungeon: 1);
        var placements = MakePlacements((RoomRole.TreasureRoom, 2));

        // Act
        var result = generator.Generate(placements, _worldId, _stateId, Random.Shared);

        // Assert
        var trap = Assert.Single(result.Triggers);
        var trapCellar = result.Rooms.Single(room => room.Name == "Trap Cellar");
        var rubbleLanding = result.Rooms.Single(room => room.Name == "Rubble Landing");
        Assert.Equal(trapCellar.LocationId, trap.TargetId);
        Assert.Equal(-1, trapCellar.FloorNumber);
        Assert.Equal(-1, rubbleLanding.FloorNumber);

        // The cellar has no way out of its own — only onward into the landing.
        var fromCellar = result.LocationConnectors.Where(connector =>
            connector.OriginLocationId == trapCellar.LocationId
        );
        Assert.All(
            fromCellar,
            connector => Assert.Equal(rubbleLanding.LocationId, connector.DestinationLocationId)
        );

        // The landing has a way back up.
        Assert.Contains(
            result.LocationConnectors,
            connector =>
                connector.OriginLocationId == rubbleLanding.LocationId
                && connector.DestinationLocationId != trapCellar.LocationId
        );
    }

    [Fact]
    public void Generate_ReusesTheSameBasement_AcrossEveryFallingTrapInOneDungeon()
    {
        // Arrange
        var generator = MakeGenerator(maxTrapsPerDungeon: 3);
        var placements = MakePlacements(
            (RoomRole.TreasureRoom, 1),
            (RoomRole.CollapsedGallery, 2),
            (RoomRole.Passage, 3)
        );

        // Act
        var result = generator.Generate(placements, _worldId, _stateId, Random.Shared);

        // Assert — exactly one Rubble Landing and one Trap Cellar, no matter how many falling
        // traps landed in this dungeon.
        Assert.Equal(3, result.Triggers.Count);
        Assert.Equal(2, result.Rooms.Count);
    }

    [Fact]
    public void Generate_TargetsAShallowerRoomOnTheSameFloor_ForWater()
    {
        var generator = MakeGenerator(maxTrapsPerDungeon: 1);
        var shallow = MakeRoom(depth: 1);
        var deep = MakeRoom(depth: 4);
        var placements = new[]
        {
            new DungeonRoomPlacement(
                deep,
                RoomRole.FloodedSump,
                4,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ),
            new DungeonRoomPlacement(
                shallow,
                RoomRole.Storeroom,
                1,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ),
        };

        // Act
        var trap = Assert.Single(
            generator.Generate(placements, _worldId, _stateId, Random.Shared).Triggers
        );

        // Assert
        Assert.Equal(shallow.LocationId, trap.TargetId);
    }

    [Fact]
    public void Generate_WaterNeverTargetsARoomOnADifferentFloor()
    {
        // Arrange — the only shallower room sits on a different floor entirely.
        var generator = MakeGenerator(maxTrapsPerDungeon: 1);
        var placements = new[]
        {
            new DungeonRoomPlacement(
                MakeRoom(depth: 4),
                RoomRole.FloodedSump,
                4,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ),
            new DungeonRoomPlacement(
                MakeRoom(depth: 1),
                RoomRole.Storeroom,
                1,
                FloorNumber: 1,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ),
        };

        // Act
        var result = generator.Generate(placements, _worldId, _stateId, Random.Shared);

        // Assert
        Assert.Empty(result.Triggers);
    }

    [Fact]
    public void Generate_WaterNeverTargetsAnotherTrapRoom()
    {
        // Arrange — two water-eligible rooms and nothing else shallower than the deepest one, so
        // the only way either trap could get a target at all is by pointing at the other.
        var generator = MakeGenerator(maxTrapsPerDungeon: 2);
        var placements = MakePlacements((RoomRole.FloodedSump, 1), (RoomRole.FloodedSump, 2));

        for (var i = 0; i < 20; i++)
        {
            // Act
            var result = generator.Generate(placements, _worldId, _stateId, Random.Shared);

            // Assert — with no other candidate, both traps must come back targetless.
            Assert.Empty(result.Triggers);
        }
    }

    [Fact]
    public void Generate_ReturnsNothing_WhenNoRoomHasAnEligibleRole()
    {
        var generator = MakeGenerator(maxTrapsPerDungeon: 2);
        var placements = MakePlacements((RoomRole.Storeroom, 1), (RoomRole.Shrine, 2));

        // Act
        var result = generator.Generate(placements, _worldId, _stateId, Random.Shared);

        // Assert
        Assert.Empty(result.Triggers);
        Assert.Empty(result.Rooms);
    }

    private static DungeonTrapGenerator MakeGenerator(int maxTrapsPerDungeon) =>
        new(
            new TestOptionsSnapshot<TrapOptions>(
                new TrapOptions { MaxTrapsPerDungeon = maxTrapsPerDungeon }
            )
        );

    private static IReadOnlyList<DungeonRoomPlacement> MakePlacements(
        params (RoomRole Role, int Depth)[] rooms
    ) =>
        rooms
            .Select(r => new DungeonRoomPlacement(
                MakeRoom(r.Depth),
                r.Role,
                r.Depth,
                FloorNumber: 0,
                RouteKind: DungeonRouteKind.None,
                IsDeadEnd: false
            ))
            .ToArray();

    private static Room MakeRoom(int depth) =>
        new()
        {
            BuildingId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Name = $"Room {depth}",
            Description = "A room.",
            WorldId = Guid.NewGuid(),
        };
}
