using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonLayoutGeneratorTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(15)]
    public void Generate_LeavesEveryRoomReachable(int roomCount)
    {
        // Arrange — a dungeon with an unreachable room is one a player can never finish.
        var input = MakeInput(roomCount);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        Assert.All(layout.Rooms, room => Assert.True(room.DepthFromEntrance >= 0));
    }

    [Fact]
    public void Generate_PutsTheBossAwayFromTheEntrance()
    {
        // Arrange
        var input = MakeInput(12);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        var boss = layout.Rooms[layout.BossIndex];
        Assert.True(boss.DepthFromEntrance > 1);
        Assert.NotEqual(layout.EntranceIndex, layout.BossIndex);
    }

    [Fact]
    public void Generate_MakesTheBossDeeperThanEveryRoutesOwnLastRoom()
    {
        // Arrange — every route converges on the boss, so a plain shortest-path depth would make
        // arriving via the longer route narrate as "back toward the way you came" instead of
        // "deeper in". The boss must read as deeper than every route, not just the nearest one.
        var input = MakeInput(15);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        var bossDepth = layout.Rooms[layout.BossIndex].DepthFromEntrance;
        var routeRooms = layout.Rooms.Where(room =>
            room.Index != layout.EntranceIndex && room.Index != layout.BossIndex
        );
        Assert.All(routeRooms, room => Assert.True(bossDepth > room.DepthFromEntrance));
    }

    [Fact]
    public void Generate_MakesTheLongAndShortRoutesDisjoint()
    {
        // Arrange — routes must share nothing but entrance and boss, or a gate on one route
        // could be walked around through the other's rooms.
        var input = MakeInput(14);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        var longRooms = RoomsOn(layout, DungeonRouteKind.Long);
        var shortRooms = RoomsOn(layout, DungeonRouteKind.Short);
        Assert.NotEmpty(longRooms);
        Assert.NotEmpty(shortRooms);
        Assert.Empty(longRooms.Intersect(shortRooms));
    }

    [Fact]
    public void Generate_AttachesDeadEndsOnlyToTheLongRoute()
    {
        // Arrange — the safe route is where exploring off the beaten path is worth the walk, not
        // a route that's already gated by its own obstacle.
        var input = MakeInput(14);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        var deadEnds = layout.Rooms.Where(room => room.IsDeadEnd).ToArray();
        Assert.All(deadEnds, room => Assert.Equal(DungeonRouteKind.Long, room.RouteKind));
        Assert.DoesNotContain(deadEnds, room => room.Index == layout.EntranceIndex);
        Assert.DoesNotContain(deadEnds, room => room.Index == layout.BossIndex);
    }

    [Fact]
    public void Generate_NeverMarksEntranceOrBossAsPartOfARoute()
    {
        // Arrange
        var input = MakeInput(12);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        Assert.Equal(DungeonRouteKind.None, layout.Rooms[layout.EntranceIndex].RouteKind);
        Assert.Equal(DungeonRouteKind.None, layout.Rooms[layout.BossIndex].RouteKind);
    }

    [Fact]
    public void Generate_KeepsRoomsApart_SoAMapCanBeDrawnFromThem()
    {
        // Arrange
        var input = MakeInput(12);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        var positions = layout.Rooms.Select(room => room.Position).Distinct().ToArray();
        Assert.Equal(layout.Rooms.Count, positions.Length);
    }

    [Fact]
    public void Generate_IsRepeatable_ForTheSameSeed()
    {
        // Arrange

        // Act
        var layout = DungeonLayoutGenerator.Generate(MakeInput(12));

        // Assert
        var same = DungeonLayoutGenerator.Generate(MakeInput(12));
        Assert.Equal(
            layout.Rooms.Select(room => room.Position),
            same.Rooms.Select(room => room.Position)
        );
        Assert.Equal(layout.EntranceIndex, same.EntranceIndex);
        Assert.Equal(layout.BossIndex, same.BossIndex);
    }

    private static IReadOnlyCollection<int> RoomsOn(DungeonLayout layout, DungeonRouteKind kind) =>
        layout.Rooms.Where(room => room.RouteKind == kind).Select(room => room.Index).ToArray();

    private static DungeonLayoutInput MakeInput(int roomCount) =>
        new(roomCount) { Random = new Random(20260908) };
}
