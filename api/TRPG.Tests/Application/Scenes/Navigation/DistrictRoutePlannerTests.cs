using TRPG.Application.Scenes.Navigation;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Navigation;

public class DistrictRoutePlannerTests
{
    private static readonly Point Port = new(0, 10);
    private static readonly Point Junction = new(20, 10);

    [Fact]
    public void Plan_WalksTheRoadThenCutsAcrossToTheAnchor()
    {
        // Arrange
        var network = MakeNetwork(RoadNodeKind.Port, Port, Junction, []);
        var grid = DistrictNavigationGrid.Build(MakeDistrict(), []);

        // Act
        var path = DistrictRoutePlanner.Plan(Port, new Point(15, 14), network, grid);

        // Assert
        Assert.Equal([Port, new Point(15, 10), new Point(15, 14)], path);
    }

    [Fact]
    public void Plan_FollowsEdgeWaypointsInOrder_WhenTheEdgeIsTraversedBackwards()
    {
        // Arrange
        var network = MakeNetwork(RoadNodeKind.Port, Port, Junction, [new Point(10, 10)], true);
        var grid = DistrictNavigationGrid.Build(MakeDistrict(), []);

        // Act
        var path = DistrictRoutePlanner.Plan(Port, new Point(15, 14), network, grid);

        // Assert
        Assert.Equal([Port, new Point(10, 10), new Point(15, 10), new Point(15, 14)], path);
    }

    [Fact]
    public void Plan_GoesAroundBuildings_WhenTheAnchorIsBehindOne()
    {
        // Arrange
        var network = MakeNetwork(RoadNodeKind.Port, Port, Junction, []);
        var building = Builders.MakeBuilding(x: 15, y: 15, width: 6, depth: 1.5);
        var grid = DistrictNavigationGrid.Build(MakeDistrict(), [building]);

        // Act
        var path = DistrictRoutePlanner.Plan(Port, new Point(15, 20), network, grid);

        // Assert
        Assert.True(path.Count > 3);
    }

    [Fact]
    public void Plan_WalksStraightAcrossOpenGround_WhenTheDistrictHasNoPort()
    {
        // Arrange
        var network = MakeNetwork(RoadNodeKind.Junction, Port, Junction, []);
        var grid = DistrictNavigationGrid.Build(MakeDistrict(), []);

        // Act
        var path = DistrictRoutePlanner.Plan(new Point(2, 2), new Point(25, 25), network, grid);

        // Assert
        Assert.Equal([new Point(2, 2), new Point(25, 25)], path);
    }

    private static Location MakeDistrict() =>
        Builders.MakeLocation(kind: LocationKind.District, width: 30, depth: 30);

    private static LocationRoadNetwork MakeNetwork(
        RoadNodeKind firstKind,
        Point first,
        Point second,
        List<Point> waypoints,
        bool reversed = false
    )
    {
        var start = new RoadNode
        {
            Kind = firstKind,
            X = first.X,
            Y = first.Y,
        };
        var end = new RoadNode
        {
            Kind = RoadNodeKind.Junction,
            X = second.X,
            Y = second.Y,
        };
        var edge = reversed
            ? new RoadEdge
            {
                FromNodeId = end.Id,
                ToNodeId = start.Id,
                Waypoints = new Polyline { Points = [.. waypoints.AsEnumerable().Reverse()] },
            }
            : new RoadEdge
            {
                FromNodeId = start.Id,
                ToNodeId = end.Id,
                Waypoints = new Polyline { Points = waypoints },
            };

        return new LocationRoadNetwork([start, end], [edge]);
    }
}
