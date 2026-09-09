using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonTrapGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();

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
            var traps = generator.Generate(placements, _worldId, Random.Shared);

            // Assert
            Assert.True(traps.Count <= 2);
        }
    }

    [Theory]
    [InlineData(RoomRole.TreasureRoom, TrapKind.Mechanical)]
    [InlineData(RoomRole.CollapsedGallery, TrapKind.Collapse)]
    [InlineData(RoomRole.Passage, TrapKind.Slope)]
    [InlineData(RoomRole.FloodedSump, TrapKind.Water)]
    public void Generate_PicksTrapKind_FromTheRoomsRole(RoomRole role, TrapKind expectedKind)
    {
        // A shallower and a deeper alternative both exist, since water targets backwards and every
        // other kind targets deeper — whichever kind is under test needs its direction covered.
        var generator = MakeGenerator(maxTrapsPerDungeon: 1);
        var trapRoom = MakeRoom(depth: 3);
        var placements = new[]
        {
            new DungeonRoomPlacement(trapRoom, role, 3),
            new DungeonRoomPlacement(MakeRoom(depth: 1), RoomRole.Storeroom, 1),
            new DungeonRoomPlacement(MakeRoom(depth: 5), RoomRole.Storeroom, 5),
        };

        // Act
        var trap = Assert.Single(generator.Generate(placements, _worldId, Random.Shared));

        // Assert
        Assert.Equal(expectedKind, trap.TrapKind);
        Assert.Equal(trapRoom.LocationId, trap.LocationId);
    }

    [Fact]
    public void Generate_TargetsADeeperRoom_ForEveryKindExceptWater()
    {
        var generator = MakeGenerator(maxTrapsPerDungeon: 1);
        var shallow = MakeRoom(depth: 1);
        var deep = MakeRoom(depth: 4);
        var placements = new[]
        {
            new DungeonRoomPlacement(shallow, RoomRole.TreasureRoom, 1),
            new DungeonRoomPlacement(deep, RoomRole.Storeroom, 4),
        };

        // Act
        var trap = Assert.Single(generator.Generate(placements, _worldId, Random.Shared));

        // Assert
        Assert.Equal(deep.LocationId, trap.TargetId);
    }

    [Fact]
    public void Generate_TargetsAShallowerRoom_ForWater()
    {
        var generator = MakeGenerator(maxTrapsPerDungeon: 1);
        var shallow = MakeRoom(depth: 1);
        var deep = MakeRoom(depth: 4);
        var placements = new[]
        {
            new DungeonRoomPlacement(deep, RoomRole.FloodedSump, 4),
            new DungeonRoomPlacement(shallow, RoomRole.Storeroom, 1),
        };

        // Act
        var trap = Assert.Single(generator.Generate(placements, _worldId, Random.Shared));

        // Assert
        Assert.Equal(shallow.LocationId, trap.TargetId);
    }

    [Fact]
    public void Generate_NeverTargetsAnotherTrapRoom()
    {
        // Arrange — two mechanical-eligible rooms and nothing else deeper than the shallowest one,
        // so the only way either trap could get a target at all is by pointing at the other.
        var generator = MakeGenerator(maxTrapsPerDungeon: 2);
        var placements = MakePlacements((RoomRole.TreasureRoom, 1), (RoomRole.TreasureRoom, 2));

        for (var i = 0; i < 20; i++)
        {
            // Act
            var traps = generator.Generate(placements, _worldId, Random.Shared);

            // Assert — with no other candidate, both traps must come back targetless.
            Assert.Empty(traps);
        }
    }

    [Fact]
    public void Generate_ReturnsNothing_WhenNoRoomHasAnEligibleRole()
    {
        var generator = MakeGenerator(maxTrapsPerDungeon: 2);
        var placements = MakePlacements((RoomRole.Storeroom, 1), (RoomRole.Shrine, 2));

        // Act
        var traps = generator.Generate(placements, _worldId, Random.Shared);

        // Assert
        Assert.Empty(traps);
    }

    private static DungeonTrapGenerator MakeGenerator(int maxTrapsPerDungeon) =>
        new(
            new TestOptionsSnapshot<TrapOptions>(
                new TrapOptions { MaxTrapsPerDungeon = maxTrapsPerDungeon }
            )
        );

    private static IReadOnlyList<DungeonRoomPlacement> MakePlacements(
        params (RoomRole Role, int Depth)[] rooms
    ) => rooms.Select(r => new DungeonRoomPlacement(MakeRoom(r.Depth), r.Role, r.Depth)).ToArray();

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
