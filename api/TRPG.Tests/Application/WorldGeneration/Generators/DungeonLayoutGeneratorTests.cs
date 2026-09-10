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

    [Fact]
    public void Generate_WhenFloorSplitOccurs_RoutesConvergeOnALandingBeforeTheBoss()
    {
        // Arrange — a Landing can also appear for a lever gate alone, so search specifically for a
        // seed where the boss actually changed floor, not just for any Landing's existence.
        var layout = FindLayout(
            15,
            candidate =>
                candidate.LandingIndex != null
                && candidate.Rooms[candidate.BossIndex].FloorNumber > 0
        );
        var landingIndex = layout.LandingIndex!.Value;
        var landing = layout.Rooms[landingIndex];
        var boss = layout.Rooms[layout.BossIndex];

        // Act & Assert
        Assert.Equal(DungeonRouteKind.None, landing.RouteKind);
        Assert.Equal(landing.DepthFromEntrance + 1, boss.DepthFromEntrance);
        Assert.True(boss.FloorNumber > landing.FloorNumber);
        Assert.Contains(
            layout.Passages,
            passage => Connects(passage, landingIndex, layout.BossIndex)
        );
        foreach (
            var kind in new[]
            {
                DungeonRouteKind.Long,
                DungeonRouteKind.Short,
                DungeonRouteKind.Third,
            }
        )
        {
            var routeRooms = RoomsOn(layout, kind);
            if (routeRooms.Count == 0)
            {
                continue;
            }

            var lastRoom = layout
                .Rooms.Where(room => routeRooms.Contains(room.Index))
                .OrderByDescending(room => room.DepthFromEntrance)
                .First();
            Assert.Contains(
                layout.Passages,
                passage => Connects(passage, lastRoom.Index, landingIndex)
            );
            Assert.DoesNotContain(
                layout.Passages,
                passage => Connects(passage, lastRoom.Index, layout.BossIndex)
            );
        }
    }

    [Fact]
    public void Generate_WhenFloorSplitDoesNotOccur_TheBossStaysOnTheGroundFloor()
    {
        // Arrange
        var layout = FindLayout(15, candidate => candidate.LandingIndex == null);

        // Act & Assert
        Assert.Equal(0, layout.Rooms[layout.BossIndex].FloorNumber);
    }

    [Fact]
    public void Generate_WhenALeverGateOccursWithoutFloorSplit_StillBuildsALanding_ButKeepsTheBossOnTheGroundFloor()
    {
        // Arrange — the floor-split staircase and the lever-gate portcullis share the same Landing
        // construct, but only floor-split ever changes the boss's floor.
        var layout = FindLayout(
            15,
            candidate =>
                candidate.LandingIndex != null
                && candidate.Rooms[candidate.BossIndex].FloorNumber == 0
        );

        // Act & Assert
        Assert.True(layout.HasLeverGate);
    }

    [Fact]
    public void Generate_AlwaysBuildsAMandatoryShortcut_DirectlyFromTheBoss()
    {
        // Arrange — ShortcutFillerChance is a private roll, so search for a seed without a filler.
        var layout = FindLayout(
            15,
            candidate =>
                candidate.Passages.Any(p =>
                    Connects(p, candidate.BossIndex, candidate.BackDoorIndex)
                )
        );

        // Act & Assert
        var backDoor = layout.Rooms[layout.BackDoorIndex];
        Assert.Equal(DungeonRouteKind.None, backDoor.RouteKind);
        Assert.False(backDoor.IsDeadEnd);
        Assert.Contains(
            layout.Passages,
            passage => Connects(passage, layout.EntranceIndex, layout.BackDoorIndex)
        );
    }

    [Fact]
    public void Generate_AlwaysBuildsAMandatoryShortcut_ThroughAFillerRoomWhenOneIsRolled()
    {
        // Arrange — the alternative to a direct Boss-to-back-door passage is exactly one filler
        // room between them, never a missing connection.
        var layout = FindLayout(
            15,
            candidate =>
                !candidate.Passages.Any(p =>
                    Connects(p, candidate.BossIndex, candidate.BackDoorIndex)
                )
        );

        // Act & Assert
        var filler = layout.Rooms.Single(room =>
            layout.Passages.Any(p => Connects(p, layout.BossIndex, room.Index))
            && layout.Passages.Any(p => Connects(p, room.Index, layout.BackDoorIndex))
        );
        Assert.Equal(DungeonRouteKind.None, filler.RouteKind);
        Assert.Contains(
            layout.Passages,
            passage => Connects(passage, layout.EntranceIndex, layout.BackDoorIndex)
        );
    }

    private static bool Connects(DungeonPassage passage, int a, int b) =>
        (passage.From == a && passage.To == b) || (passage.From == b && passage.To == a);

    private static DungeonLayout FindLayout(int roomCount, Func<DungeonLayout, bool> matches)
    {
        for (var seed = 0; seed < 2000; seed++)
        {
            var layout = DungeonLayoutGenerator.Generate(
                new DungeonLayoutInput(roomCount) { Random = new Random(seed) }
            );
            if (matches(layout))
            {
                return layout;
            }
        }

        throw new InvalidOperationException(
            "No seed within range produced a layout matching the requested condition."
        );
    }

    private static IReadOnlyCollection<int> RoomsOn(DungeonLayout layout, DungeonRouteKind kind) =>
        layout.Rooms.Where(room => room.RouteKind == kind).Select(room => room.Index).ToArray();

    private static DungeonLayoutInput MakeInput(int roomCount) =>
        new(roomCount) { Random = new Random(20260908) };
}
