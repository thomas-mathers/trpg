using TRPG.Application.Scenes.Roads;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes.Roads;

public class DistrictRoadPlannerTests
{
    private static readonly Footprint Size = new(40, 30);

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

    private static RoadTerminal Terminal(double startX, double startY, double angle) =>
        new(
            new Point(startX, startY),
            new Point(startX + 1.5 * Math.Sin(angle), startY - 1.5 * Math.Cos(angle))
        );

    private static int ConnectedGroups(IReadOnlyList<DistrictRoad> roads)
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

    private static bool Touches(DistrictRoad first, DistrictRoad second) =>
        Sample(first).Any(point => Sample(second).Any(other => Distance(point, other) < 0.8));

    private static IEnumerable<Point> Sample(DistrictRoad road) =>
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

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
