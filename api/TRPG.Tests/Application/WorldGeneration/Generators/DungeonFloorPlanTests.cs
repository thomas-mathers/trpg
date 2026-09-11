using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonFloorPlanTests
{
    public static IEnumerable<object[]> Seeds =>
        Enumerable.Range(0, 200).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate_PreservesClearanceAndIndependentPassages_AcrossSeededLayouts(int seed)
    {
        // Arrange
        var input = Input(seed);

        // Act
        var result = DungeonGenerator.Generate(input);

        // Assert
        AssertFloorPlan(result.Rooms, result.LocationConnectors);
        var byLocation = result.Rooms.ToDictionary(room => room.LocationId);
        foreach (
            var connector in result.LocationConnectors.Where(connector =>
                byLocation.ContainsKey(connector.OriginLocationId)
                && byLocation.ContainsKey(connector.DestinationLocationId)
            )
        )
        {
            var origin = byLocation[connector.OriginLocationId];
            var destination = byLocation[connector.DestinationLocationId];
            if (origin.FloorNumber != destination.FloorNumber)
            {
                Assert.Null(connector.Path);
                Assert.Null(connector.Direction);
                Assert.Equal("Stairway", connector.Name);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate_PreservesClearance_WhenAddingLeverShortcuts(int seed)
    {
        // Arrange
        var dungeon = DungeonGenerator.Generate(Input(seed));
        var generator = new DungeonObstacleGenerator(
            new DungeonPopulator(Builders.MakeCreatureGenerator())
        );
        var input = ObstacleInput(dungeon, seed);

        // Act
        var obstacle = generator.Generate(input, DungeonObstacleKind.LeverShortcut);

        // Assert
        AssertFloorPlan(
            dungeon.Rooms.Concat(obstacle.Rooms).ToArray(),
            dungeon.LocationConnectors.Concat(obstacle.LocationConnectors).ToArray()
        );
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate_PreservesClearance_WhenAddingKeySpurs(int seed)
    {
        // Arrange
        var dungeon = DungeonGenerator.Generate(Input(seed));
        var generator = new DungeonObstacleGenerator(
            new DungeonPopulator(Builders.MakeCreatureGenerator())
        );
        var input = ObstacleInput(dungeon, seed);

        // Act
        var obstacle = generator.Generate(input, DungeonObstacleKind.KeyLock);

        // Assert
        AssertFloorPlan(
            dungeon.Rooms.Concat(obstacle.Rooms).ToArray(),
            dungeon.LocationConnectors.Concat(obstacle.LocationConnectors).ToArray()
        );
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate_KeepsCorridorsCompact_RelativeToRoomSizes(int seed)
    {
        // Arrange
        var input = Input(seed);

        // Act
        var dungeon = DungeonGenerator.Generate(input);

        // Assert
        AssertCompactCorridors(dungeon);
        AssertDirectShortRoute(dungeon);
    }

    private static void AssertCompactCorridors(DungeonGeneratorResult dungeon)
    {
        var averageRoomWidth = dungeon.Rooms.Average(room => room.Bounds!.Right - room.Bounds.Left);
        var averageCorridorLength = dungeon
            .LocationConnectors.Where(connector => connector.Path != null)
            .Average(connector =>
                Segments(connector.Path!)
                    .Sum(segment =>
                        Math.Abs(segment.Start.X - segment.End.X)
                        + Math.Abs(segment.Start.Y - segment.End.Y)
                    )
            );
        Assert.True(
            averageCorridorLength < 2 * averageRoomWidth,
            $"Average corridor length {averageCorridorLength:F1} exceeds twice the room width {averageRoomWidth:F1}"
        );
    }

    private static void AssertDirectShortRoute(DungeonGeneratorResult dungeon)
    {
        var hasThirdRoute = dungeon.Placements.Any(room =>
            room.RouteKind == DungeonRouteKind.Third
        );
        if (hasThirdRoute)
            return;
        var shortRooms = dungeon
            .Placements.Where(room => room.RouteKind == DungeonRouteKind.Short)
            .Select(room => room.Room.LocationId)
            .ToHashSet();
        foreach (
            var passage in dungeon.LocationConnectors.Where(connector =>
                connector.Path != null
                && (
                    shortRooms.Contains(connector.OriginLocationId)
                    || shortRooms.Contains(connector.DestinationLocationId)
                )
            )
        )
        {
            Assert.Equal(2, passage.Path!.Points.Count);
            Assert.InRange(Math.Abs(passage.Path.Points[0].X - passage.Path.Points[1].X), 6, 44);
        }
    }

    private static DungeonGeneratorInput Input(int seed) =>
        new(
            [],
            new Location
            {
                StateId = Guid.NewGuid(),
                WorldId = Guid.NewGuid(),
                Kind = LocationKind.Wilderness,
            },
            Guid.NewGuid()
        )
        {
            Random = new Random(seed),
        };

    private static DungeonObstacleInput ObstacleInput(DungeonGeneratorResult dungeon, int seed) =>
        new(
            dungeon.Placements,
            dungeon.LocationConnectors,
            dungeon.EntranceLocationId,
            dungeon.BossLocationId,
            dungeon.Building.Id,
            dungeon.Building.BuildingType,
            dungeon.Building.WorldId,
            Guid.NewGuid(),
            EncounterFactionGenerator.Generate(dungeon.Building.WorldId),
            new Random(seed)
        );

    private static void AssertFloorPlan(
        IReadOnlyList<Room> rooms,
        IReadOnlyList<LocationConnector> connectors
    )
    {
        foreach (var floor in rooms.GroupBy(room => room.FloorNumber))
        {
            var roomArray = floor.ToArray();
            AssertSeparateRooms(roomArray);
            var byLocation = roomArray.ToDictionary(room => room.LocationId);
            var passages = connectors
                .Where(connector =>
                    byLocation.ContainsKey(connector.OriginLocationId)
                    && byLocation.ContainsKey(connector.DestinationLocationId)
                )
                .DistinctBy(connector =>
                    string.Join(
                        "/",
                        new[]
                        {
                            connector.OriginLocationId,
                            connector.DestinationLocationId,
                        }.Order()
                    )
                )
                .ToArray();
            foreach (var passage in passages)
                AssertPassage(passage, byLocation);
            AssertSeparatePassages(passages, byLocation);
        }
    }

    private static void AssertSeparateRooms(IReadOnlyList<Room> rooms)
    {
        for (var index = 0; index < rooms.Count; index++)
        {
            foreach (var other in rooms.Skip(index + 1))
            {
                Assert.False(
                    Overlaps(rooms[index].Bounds!, other.Bounds!),
                    $"Overlapping rooms: {rooms[index].Name}, {other.Name}"
                );
            }
        }
    }

    private static void AssertSeparatePassages(
        IReadOnlyList<LocationConnector> passages,
        IReadOnlyDictionary<Guid, Room> rooms
    )
    {
        for (var index = 0; index < passages.Count; index++)
            foreach (var other in passages.Skip(index + 1))
            foreach (var segment in Segments(passages[index].Path!))
            foreach (var otherSegment in Segments(other.Path!))
            {
                Assert.False(
                    Overlaps(Envelope(segment), Envelope(otherSegment)),
                    $"Corridor collision: {rooms[passages[index].OriginLocationId].Name}–{rooms[passages[index].DestinationLocationId].Name} / {rooms[other.OriginLocationId].Name}–{rooms[other.DestinationLocationId].Name}"
                );
            }
    }

    private static void AssertPassage(
        LocationConnector passage,
        IReadOnlyDictionary<Guid, Room> rooms
    )
    {
        Assert.NotNull(passage.Path);
        Assert.True(passage.Path.Points.Count >= 2);
        AssertDoorway(rooms[passage.OriginLocationId].Bounds!, passage.Path.Points[0]);
        AssertDoorway(rooms[passage.DestinationLocationId].Bounds!, passage.Path.Points[^1]);
        foreach (var segment in Segments(passage.Path))
        {
            Assert.True(segment.Start.X == segment.End.X || segment.Start.Y == segment.End.Y);
            Assert.True(
                Math.Abs(segment.Start.X - segment.End.X)
                    + Math.Abs(segment.Start.Y - segment.End.Y)
                    >= 6,
                $"Tiny corridor segment: {rooms[passage.OriginLocationId].Name}–{rooms[passage.DestinationLocationId].Name}, {segment.Start} → {segment.End}"
            );
            foreach (
                var room in rooms.Values.Where(room =>
                    room.LocationId != passage.OriginLocationId
                    && room.LocationId != passage.DestinationLocationId
                )
            )
                Assert.False(
                    Overlaps(Envelope(segment), room.Bounds!),
                    $"Corridor {rooms[passage.OriginLocationId].Name}–{rooms[passage.DestinationLocationId].Name} intersects {room.Name}"
                );
        }
    }

    private static void AssertDoorway(Rectangle bounds, Point point)
    {
        Assert.InRange(point.X, bounds.Left, bounds.Right);
        Assert.InRange(point.Y, bounds.Top, bounds.Bottom);
        Assert.True(
            point.X == bounds.Left
                || point.X == bounds.Right
                || point.Y == bounds.Top
                || point.Y == bounds.Bottom
        );
    }

    private sealed record Segment(Point Start, Point End);

    private static IEnumerable<Segment> Segments(Polyline path) =>
        path.Points.Zip(path.Points.Skip(1), (start, end) => new Segment(start, end));

    private static Rectangle Envelope(Segment segment) =>
        new(
            (int)Math.Floor(Math.Min(segment.Start.X, segment.End.X) - 2),
            (int)Math.Floor(Math.Min(segment.Start.Y, segment.End.Y) - 2),
            (int)Math.Ceiling(Math.Max(segment.Start.X, segment.End.X) + 2),
            (int)Math.Ceiling(Math.Max(segment.Start.Y, segment.End.Y) + 2)
        );

    private static bool Overlaps(Rectangle first, Rectangle second) =>
        first.Left < second.Right
        && first.Right > second.Left
        && first.Top < second.Bottom
        && first.Bottom > second.Top;
}
