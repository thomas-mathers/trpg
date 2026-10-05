using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictRoadPlannerTests
{
    private static readonly Footprint Size = new(40, 30);
    private static readonly Footprint GatedSize = new(103.5, 82.5);

    [Fact]
    public void Plan_ReturnsNoRoads_WhenThereAreNoTerminals()
    {
        // Arrange
        RoadTerminal[] terminals = [];

        // Act
        var roads = DistrictRoadPlanner.Plan(Size, [], terminals);

        // Assert
        Assert.Empty(roads);
    }

    [Fact]
    public void Plan_StartsEachRoadAtItsTerminal()
    {
        // Arrange
        RoadTerminal[] terminals =
        [
            Terminal(startX: 20, startY: 0, angle: Math.PI),
            Terminal(startX: 0, startY: 15, angle: Math.PI / 2),
            Terminal(startX: 40, startY: 15, angle: 3 * Math.PI / 2),
        ];

        // Act
        var roads = DistrictRoadPlanner.Plan(Size, [], terminals);

        // Assert
        Assert.Equal(
            terminals.Select(terminal => terminal.Start).ToHashSet(),
            roads.Select(road => road.Points[0]).ToHashSet()
        );
    }

    [Fact]
    public void Plan_JoinsEveryRoadToTheSameNetwork()
    {
        // Arrange
        RoadTerminal[] terminals =
        [
            Terminal(startX: 20, startY: 0, angle: Math.PI),
            Terminal(startX: 20, startY: 30, angle: 0),
            Terminal(startX: 0, startY: 15, angle: Math.PI / 2),
            Terminal(startX: 40, startY: 15, angle: 3 * Math.PI / 2),
        ];

        // Act
        var roads = DistrictRoadPlanner.Plan(Size, [], terminals);

        // Assert
        Assert.Equal(1, ConnectedGroups(roads));
    }

    [Fact]
    public void Plan_RoutesAroundABuildingBlockingTheDirectLine()
    {
        // Arrange
        var building = new RoadBuilding(new Placement(20, 15, 0), new Footprint(8, 6));
        RoadTerminal[] terminals =
        [
            Terminal(startX: 20, startY: 0, angle: Math.PI),
            Terminal(startX: 20, startY: 30, angle: 0),
        ];

        // Act
        var roads = DistrictRoadPlanner.Plan(Size, [building], terminals);

        // Assert
        Assert.Equal(1, ConnectedGroups(roads));
        Assert.All(
            roads.SelectMany(Sample),
            point =>
                Assert.False(
                    Math.Abs(point.X - 20) < 4 && Math.Abs(point.Y - 15) < 3,
                    $"Road crosses the building at ({point.X}, {point.Y})."
                )
        );
    }

    [Fact]
    public void Plan_ProducesTheSameRoads_WhenRunTwice()
    {
        // Arrange
        var building = new RoadBuilding(new Placement(20, 15, 0), new Footprint(8, 6));
        RoadTerminal[] terminals =
        [
            Terminal(startX: 20, startY: 0, angle: Math.PI),
            Terminal(startX: 0, startY: 15, angle: Math.PI / 2),
        ];
        var first = DistrictRoadPlanner.Plan(Size, [building], terminals);

        // Act
        var second = DistrictRoadPlanner.Plan(Size, [building], terminals);

        // Assert
        Assert.Equal(first.SelectMany(road => road.Points), second.SelectMany(road => road.Points));
    }

    [Fact]
    public void Plan_EndsEachJoiningRoadOnTheRoadItMeets_ForAFullDistrict()
    {
        // Arrange
        RoadBuilding[] buildings =
        [
            new(new Placement(20, 65, Math.PI / 2), new Footprint(12, 14)),
            new(new Placement(90, 77, 3 * Math.PI / 2), new Footprint(9, 12)),
            new(new Placement(17, 83, Math.PI / 2), new Footprint(16, 20)),
            new(new Placement(91.5, 50, 3 * Math.PI / 2), new Footprint(11, 15)),
            new(new Placement(91, 64, 3 * Math.PI / 2), new Footprint(9, 14)),
            new(new Placement(43.5, 20, Math.PI), new Footprint(25, 22)),
            new(new Placement(18, 45, Math.PI / 2), new Footprint(20, 18)),
            new(new Placement(70, 19, Math.PI), new Footprint(20, 24)),
        ];
        RoadTerminal[] terminals =
        [
            Terminal(0, 54.5, Math.PI / 2),
            Terminal(27, 45, Math.PI / 2),
            Terminal(27, 65, Math.PI / 2),
            Terminal(27, 83, Math.PI / 2),
            Terminal(37, 0, Math.PI),
            Terminal(43.5, 31, Math.PI),
            Terminal(55.5, 109, 0),
            Terminal(70, 31, Math.PI),
            Terminal(74, 0, Math.PI),
            Terminal(84, 50, 3 * Math.PI / 2),
            Terminal(84, 64, 3 * Math.PI / 2),
            Terminal(84, 77, 3 * Math.PI / 2),
            Terminal(111, 36.333333333333336, 3 * Math.PI / 2),
            Terminal(111, 72.66666666666667, 3 * Math.PI / 2),
        ];

        // Act
        var roads = DistrictRoadPlanner.Plan(new Footprint(111, 109), buildings, terminals);

        // Assert
        Assert.All(
            roads.Skip(1).Select((road, index) => (road, earlier: roads.Take(index + 1))),
            joined =>
                Assert.Contains(
                    joined.earlier,
                    earlier => DistanceToRoad(joined.road.Points[^1], earlier) < 1e-6
                )
        );
    }

    [Fact]
    public void Plan_LeavesNoDeadEndSpur_ForAFullDistrict()
    {
        // Arrange
        var roads = PlanRiverspurMerchantQuarter();

        // Act
        var trunkEnd = roads[0].Points[^1];

        // Assert
        Assert.Contains(roads.Skip(1), road => Distance(road.Points[^1], trunkEnd) < 1e-6);
    }

    [Fact]
    public void Plan_KeepsEveryBendAwayFromTheDistrictEdge_WhenGatesOpenOnAllFourEdges()
    {
        // Arrange
        var roads = PlanGatedDistrict();

        // Act
        var bends = roads.SelectMany(road => road.Points.Skip(1)).ToArray();

        // Assert
        Assert.All(
            bends,
            point =>
                Assert.True(
                    Math.Min(
                        Math.Min(point.X, GatedSize.Width - point.X),
                        Math.Min(point.Y, GatedSize.Depth - point.Y)
                    )
                        >= 2.25 - 1e-6,
                    $"Road reaches ({point.X}, {point.Y}) beside the district edge."
                )
        );
    }

    [Fact]
    public void Plan_RunsEachGateRoadStraightToTheCrossroads_WhenDoorsPullTheCentroidOffCentre()
    {
        // Arrange
        var roads = PlanGatedDistrict();

        // Act
        var gateRoads = roads.Where(road => road.Terminal.IsGate).ToArray();

        // Assert
        Assert.All(
            gateRoads,
            road =>
            {
                var first = road.Points[0];
                var onAxis = first.X == 0 || first.X == GatedSize.Width ? 41.25 : 51.75;
                var alongX = first.X == 0 || first.X == GatedSize.Width;

                Assert.All(
                    road.Points,
                    point => Assert.Equal(onAxis, alongX ? point.Y : point.X, precision: 6)
                );
            }
        );
    }

    [Fact]
    public void Plan_KeepsEveryRoadClearOfTheDistrictEdge_ExceptTheGateStems_WhenGatesAreOffCentre()
    {
        // Arrange
        RoadTerminal[] terminals =
        [
            Gate(startX: 21.75, startY: 0, angle: Math.PI),
            Gate(startX: 60.75, startY: 82.5, angle: 0),
            Gate(startX: 103.5, startY: 60.75, angle: 3 * Math.PI / 2),
        ];

        // Act
        var roads = DistrictRoadPlanner.Plan(GatedSize, [], terminals);

        // Assert
        Assert.All(
            roads.SelectMany(road => road.Points.Skip(1)),
            point =>
                Assert.True(
                    Math.Min(
                        Math.Min(point.X, GatedSize.Width - point.X),
                        Math.Min(point.Y, GatedSize.Depth - point.Y)
                    ) >= 2.45
                        || terminals.Any(terminal => IsOnStem(terminal, point)),
                    $"Road reaches ({point.X}, {point.Y}) beside the district edge."
                )
        );
    }

    private static bool IsOnStem(RoadTerminal terminal, Point point) =>
        terminal.Start.X == point.X || terminal.Start.Y == point.Y;

    private static IReadOnlyList<PlannedRoad> PlanGatedDistrict()
    {
        RoadTerminal[] terminals =
        [
            Gate(startX: 0, startY: 41.25, angle: Math.PI / 2),
            Gate(startX: 103.5, startY: 41.25, angle: 3 * Math.PI / 2),
            Gate(startX: 51.75, startY: 0, angle: Math.PI),
            Gate(startX: 51.75, startY: 82.5, angle: 0),
            Door(startX: 90.75, startY: 56.25),
            Door(startX: 90.75, startY: 65.25),
            Door(startX: 90.75, startY: 74.25),
            Door(startX: 80.25, startY: 74.25),
        ];

        return DistrictRoadPlanner.Plan(GatedSize, [], terminals);
    }

    private static RoadTerminal Gate(double startX, double startY, double angle) =>
        Entering(IsGate: true, startX, startY, angle);

    private static RoadTerminal Door(double startX, double startY) =>
        Entering(IsGate: false, startX, startY, Math.PI / 2);

    private static RoadTerminal Entering(bool IsGate, double startX, double startY, double angle) =>
        new(
            Guid.NewGuid(),
            IsGate,
            new Point(startX, startY),
            new Point(startX + 0.75 * Math.Sin(angle), startY - 0.75 * Math.Cos(angle))
        );

    private static IReadOnlyList<PlannedRoad> PlanRiverspurMerchantQuarter()
    {
        RoadBuilding[] buildings =
        [
            new(new Placement(19.5, 40.5, Math.PI / 2), new Footprint(11, 15)),
            new(new Placement(89, 61.5, 3 * Math.PI / 2), new Footprint(16, 20)),
            new(new Placement(65, 22, Math.PI), new Footprint(20, 18)),
            new(new Placement(21, 67.5, Math.PI / 2), new Footprint(9, 12)),
            new(new Placement(41, 19, Math.PI), new Footprint(20, 24)),
            new(new Placement(20, 54.5, Math.PI / 2), new Footprint(9, 14)),
            new(new Placement(86, 43.5, 3 * Math.PI / 2), new Footprint(12, 14)),
        ];
        RoadTerminal[] terminals =
        [
            Terminal(27, 40.5, Math.PI / 2),
            Terminal(106, 45, 3 * Math.PI / 2),
            Terminal(79, 61.5, 3 * Math.PI / 2),
            Terminal(27, 67.5, Math.PI / 2),
            Terminal(35.333333333333336, 0, Math.PI),
            Terminal(27, 54.5, Math.PI / 2),
            Terminal(79, 43.5, 3 * Math.PI / 2),
            Terminal(0, 45, Math.PI / 2),
            Terminal(53, 90, 0),
            Terminal(70.66666666666667, 0, Math.PI),
            Terminal(65, 31, Math.PI),
            Terminal(41, 31, Math.PI),
        ];

        return DistrictRoadPlanner.Plan(new Footprint(106, 90), buildings, terminals);
    }

    private static RoadTerminal Terminal(double startX, double startY, double angle) =>
        new(
            Guid.NewGuid(),
            IsGate: false,
            new Point(startX, startY),
            new Point(startX + 1.5 * Math.Sin(angle), startY - 1.5 * Math.Cos(angle))
        );

    private static int ConnectedGroups(IReadOnlyList<PlannedRoad> roads)
    {
        var group = Enumerable.Range(0, roads.Count).ToArray();

        for (var a = 0; a < roads.Count; a++)
        {
            for (var b = a + 1; b < roads.Count; b++)
            {
                if (Touches(roads[a], roads[b]))
                {
                    var from = group[b];
                    group = group.Select(g => g == from ? group[a] : g).ToArray();
                }
            }
        }

        return group.Distinct().Count();
    }

    private static bool Touches(PlannedRoad first, PlannedRoad second) =>
        Sample(first).Any(point => Sample(second).Any(other => Distance(point, other) < 0.8));

    private static IEnumerable<Point> Sample(PlannedRoad road) =>
        road
            .Points.Zip(road.Points.Skip(1))
            .SelectMany(pair =>
                Enumerable
                    .Range(0, 41)
                    .Select(step =>
                    {
                        var t = step / 40.0;
                        return new Point(
                            pair.First.X + (pair.Second.X - pair.First.X) * t,
                            pair.First.Y + (pair.Second.Y - pair.First.Y) * t
                        );
                    })
            );

    private static double DistanceToRoad(Point point, PlannedRoad road) =>
        road
            .Points.Zip(road.Points.Skip(1))
            .Min(pair => DistanceToSegment(point, pair.First, pair.Second));

    private static double DistanceToSegment(Point point, Point from, Point to)
    {
        var lengthSquared = Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2);
        var t = Math.Clamp(
            ((point.X - from.X) * (to.X - from.X) + (point.Y - from.Y) * (to.Y - from.Y))
                / lengthSquared,
            0,
            1
        );

        return Distance(
            point,
            new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t)
        );
    }

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
