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

        // Assert — the way in and the way to the boss being neighbours would leave no dungeon.
        var boss = layout.Rooms[layout.BossIndex];
        Assert.True(boss.DepthFromEntrance > 1);
        Assert.NotEqual(layout.EntranceIndex, layout.BossIndex);
    }

    [Fact]
    public void Generate_LeavesLoops_SoTheMapIsMoreThanATree()
    {
        // Arrange
        var input = MakeInput(14);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert — a spanning tree has exactly one fewer edge than it has rooms.
        Assert.True(layout.Passages.Count > layout.Rooms.Count - 1);
    }

    [Fact]
    public void Generate_KeepsLoopsSparse_SoTheDungeonDoesNotBecomeAnOpenField()
    {
        // Arrange
        var input = MakeInput(14);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        var loopEdges = layout.Passages.Count - (layout.Rooms.Count - 1);
        Assert.InRange(loopEdges, 1, layout.Rooms.Count / 2);
    }

    [Fact]
    public void Generate_FindsDeadEnds_WhichAreWhereExploringHasToPay()
    {
        // Arrange
        var input = MakeInput(14);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        var deadEnds = layout.Rooms.Where(room => room.IsDeadEnd).ToArray();
        Assert.True(deadEnds.Length >= 2);
        Assert.DoesNotContain(deadEnds, room => room.Index == layout.EntranceIndex);
        Assert.DoesNotContain(deadEnds, room => room.Index == layout.BossIndex);
    }

    [Fact]
    public void Generate_KeepsRoomsApart_SoAMapCanBeDrawnFromThem()
    {
        // Arrange
        var input = MakeInput(12);

        // Act
        var layout = DungeonLayoutGenerator.Generate(input);

        // Assert
        var positions = layout.Rooms.Select(room => room.Position).ToArray();
        var pairs =
            from first in positions
            from second in positions
            where !ReferenceEquals(first, second)
            select Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
        Assert.All(pairs, distance => Assert.True(distance >= 10));
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

    private static DungeonLayoutInput MakeInput(int roomCount) =>
        new(roomCount) { Random = new Random(20260908) };
}
