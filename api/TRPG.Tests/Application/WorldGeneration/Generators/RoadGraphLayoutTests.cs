using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoadGraphLayoutTests
{
    [Fact]
    public void Generate_PlansRoadConnectorsOnlyInsideDistricts()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var districtIds = world
            .Input.Locations.Where(location => location.Kind == LocationKind.District)
            .Select(location => location.Id)
            .ToHashSet();
        var roads = RoadConnectors(layout);
        var nodeById = layout.TravelNodes.ToDictionary(node => node.Id);
        Assert.NotEmpty(roads);
        Assert.All(roads, road => Assert.Contains(road.LocationId, districtIds));
        Assert.All(
            roads,
            road => Assert.Equal(road.LocationId, nodeById[road.OriginNodeId].LocationId)
        );
    }

    [Fact]
    public void Generate_PlacesEveryDistrictExitOnANodeInsideItsDistrict()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var nodeById = layout.TravelNodes.ToDictionary(node => node.Id);
        var exits = world.Input.Connectors.Where(connector =>
            world.LocationById(connector.OriginLocationId).Kind == LocationKind.District
        );
        Assert.NotEmpty(exits);
        Assert.All(
            exits,
            exit => Assert.Equal(exit.OriginLocationId, nodeById[exit.OriginNodeId].LocationId)
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_PlansRoadsWithoutSidestepsShorterThanThreeMeters(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var sidesteps = RoadSidesteps(layout);
        Assert.True(sidesteps.Count == 0, string.Join(Environment.NewLine, sidesteps));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_PlansOnlyAxisAlignedRoadSegments(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var diagonals = RoadPolylines(layout)
            .Where(points =>
                points
                    .Zip(points.Skip(1))
                    .Any(pair =>
                        Math.Abs(pair.First.X - pair.Second.X) > 1e-6
                        && Math.Abs(pair.First.Y - pair.Second.Y) > 1e-6
                    )
            )
            .Select(points =>
                "diagonal in: "
                + string.Join(" > ", points.Select(point => $"({point.X:0.###}, {point.Y:0.###})"))
            )
            .ToList();
        Assert.True(diagonals.Count == 0, string.Join(Environment.NewLine, diagonals));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_PlansEveryRoadPointOnTheHalfCellLattice(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var offLattice = RoadPolylines(layout)
            .SelectMany(points => points)
            .Where(point =>
                !CityGridLayoutTests.IsMultipleOf(point.X, CityGrid.CellSize / 2)
                || !CityGridLayoutTests.IsMultipleOf(point.Y, CityGrid.CellSize / 2)
            )
            .Select(point => $"({point.X}, {point.Y})")
            .ToList();
        Assert.True(offLattice.Count == 0, string.Join(Environment.NewLine, offLattice));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_KeepsRoadFootprintsClearOfBuildings(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var overlaps = RoadFootprints(layout)
            .SelectMany(road =>
                world
                    .Input.Buildings.Where(building =>
                        building.ExteriorLocationId == road.LocationId
                    )
                    .Where(building => Overlaps(road.Bounds, BuildingBounds(building)))
                    .Select(building => $"{road.Class} road overlaps {building.BuildingType}")
            )
            .ToList();
        Assert.True(overlaps.Count == 0, string.Join(Environment.NewLine, overlaps));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_PlansRoadsThatCrossOnlyAtJunctions(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var crossings = RoadCrossings(layout);
        Assert.True(crossings.Count == 0, string.Join(Environment.NewLine, crossings));
    }

    private static List<string> RoadCrossings(LocationLayoutResult layout)
    {
        var nodes = layout.TravelNodes.ToDictionary(node => node.Id);
        var polylines = RoadConnectors(layout)
            .Select(edge => (Edge: edge, Points: RoadPolyline(nodes, edge)))
            .ToList();

        return polylines
            .SelectMany(
                (first, index) =>
                    polylines
                        .Skip(index + 1)
                        .Where(second => first.Edge.LocationId == second.Edge.LocationId)
                        .Where(second => CrossesBetweenEnds(first.Points, second.Points))
                        .Select(second =>
                            $"crossing: {Describe(first.Points)} x {Describe(second.Points)}"
                        )
            )
            .ToList();
    }

    private static bool CrossesBetweenEnds(List<Point> first, List<Point> second) =>
        first
            .Zip(first.Skip(1))
            .Any(a =>
                second
                    .Zip(second.Skip(1))
                    .Any(b => SegmentsCrossStrictly(a.First, a.Second, b.First, b.Second))
            );

    private static bool SegmentsCrossStrictly(Point a1, Point a2, Point b1, Point b2)
    {
        var aVertical = Math.Abs(a1.X - a2.X) < 1e-6;
        var bVertical = Math.Abs(b1.X - b2.X) < 1e-6;

        if (aVertical == bVertical)
        {
            return false;
        }

        var (vertical1, vertical2, horizontal1, horizontal2) = aVertical
            ? (a1, a2, b1, b2)
            : (b1, b2, a1, a2);

        return Between(vertical1.X, horizontal1.X, horizontal2.X)
            && Between(horizontal1.Y, vertical1.Y, vertical2.Y);
    }

    private static bool Between(double value, double end, double otherEnd) =>
        value > Math.Min(end, otherEnd) + 1e-6 && value < Math.Max(end, otherEnd) - 1e-6;

    private static string Describe(List<Point> points) =>
        string.Join(">", points.Select(point => $"({point.X:0.##},{point.Y:0.##})"));

    private static List<PointConnector> RoadConnectors(LocationLayoutResult layout) =>
        [.. layout.PointConnectors.Where(connector => connector.RoadClass != null)];

    private static List<Point> RoadPolyline(
        Dictionary<Guid, TravelNode> nodes,
        PointConnector edge
    ) =>
        new[] { nodes[edge.OriginNodeId].Position }
            .Concat(edge.Waypoints.Points)
            .Append(nodes[edge.DestinationNodeId].Position)
            .ToList();

    private static List<RoadFootprint> RoadFootprints(LocationLayoutResult layout)
    {
        var nodes = layout.TravelNodes.ToDictionary(node => node.Id);
        var footprints = new List<RoadFootprint>();

        foreach (var edge in RoadConnectors(layout))
        {
            var half = RoadClassWidths.Of(edge.RoadClass!.Value) / 2;
            var points = RoadPolyline(nodes, edge);

            foreach (var (segment, (from, to)) in points.Zip(points.Skip(1)).Index())
            {
                var bounds = SegmentBounds(from, to, segment == 0 ? 0 : half, half, half);
                footprints.Add(new RoadFootprint(edge.LocationId, edge.RoadClass!.Value, bounds));
            }
        }

        return footprints;
    }

    private static Bounds SegmentBounds(
        Point from,
        Point to,
        double startReach,
        double endReach,
        double half
    )
    {
        var alongX = Math.Abs(to.X - from.X) > Math.Abs(to.Y - from.Y);
        var sign = alongX ? Math.Sign(to.X - from.X) : Math.Sign(to.Y - from.Y);
        var start = alongX ? from.X - sign * startReach : from.Y - sign * startReach;
        var end = alongX ? to.X + sign * endReach : to.Y + sign * endReach;
        var low = Math.Min(start, end);
        var high = Math.Max(start, end);

        return alongX
            ? new Bounds(low, from.Y - half, high, from.Y + half)
            : new Bounds(from.X - half, low, from.X + half, high);
    }

    private static Bounds BuildingBounds(Building building)
    {
        var sideways = Math.Abs(Math.Round(building.Angle / (Math.PI / 2))) % 2 == 1;
        var halfWidth = (sideways ? building.Depth : building.Width) / 2;
        var halfDepth = (sideways ? building.Width : building.Depth) / 2;

        return new Bounds(
            building.X - halfWidth,
            building.Y - halfDepth,
            building.X + halfWidth,
            building.Y + halfDepth
        );
    }

    private static bool Overlaps(Bounds first, Bounds second) =>
        first.MinX < second.MaxX - 1e-6
        && second.MinX < first.MaxX - 1e-6
        && first.MinY < second.MaxY - 1e-6
        && second.MinY < first.MaxY - 1e-6;

    private record Bounds(double MinX, double MinY, double MaxX, double MaxY);

    private record RoadFootprint(Guid LocationId, RoadClass Class, Bounds Bounds);

    private static List<string> RoadSidesteps(LocationLayoutResult layout)
    {
        var sidesteps = new List<string>();

        foreach (var points in RoadPolylines(layout))
        {
            for (var index = 0; index + 3 < points.Count; index++)
            {
                if (
                    IsSidestep(
                        points[index],
                        points[index + 1],
                        points[index + 2],
                        points[index + 3]
                    )
                )
                {
                    sidesteps.Add(
                        $"sidestep at {index + 1}: "
                            + string.Join(
                                " > ",
                                points.Select(point => $"({point.X:0.##}, {point.Y:0.##})")
                            )
                    );
                }
            }
        }

        return sidesteps;
    }

    private static List<List<Point>> RoadPolylines(LocationLayoutResult layout)
    {
        var nodes = layout.TravelNodes.ToDictionary(node => node.Id);

        return [.. RoadConnectors(layout).Select(edge => RoadPolyline(nodes, edge))];
    }

    private static bool IsSidestep(Point before, Point start, Point end, Point after)
    {
        var lateral = Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y);
        var alongBefore =
            (start.X - before.X) * (end.X - start.X) + (start.Y - before.Y) * (end.Y - start.Y);
        var alongAfter =
            (after.X - end.X) * (end.X - start.X) + (after.Y - end.Y) * (end.Y - start.Y);

        return lateral < 3
            && alongBefore == 0
            && alongAfter == 0
            && (start.X - before.X) * (after.X - end.X) + (start.Y - before.Y) * (after.Y - end.Y)
                > 0;
    }

    [Fact]
    public void Generate_FormsOneTreePerDistrict()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var roads = RoadConnectors(layout);
        var nodeCounts = roads
            .GroupBy(road => road.LocationId)
            .Select(group => new
            {
                Edges = group.Count(),
                Nodes = group
                    .SelectMany(road => new[] { road.OriginNodeId, road.DestinationNodeId })
                    .Distinct()
                    .Count(),
            })
            .ToArray();
        Assert.NotEmpty(nodeCounts);
        Assert.All(nodeCounts, count => Assert.Equal(count.Nodes - 1, count.Edges));
    }
}
