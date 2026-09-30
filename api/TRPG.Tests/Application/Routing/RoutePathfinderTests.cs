using TRPG.Application.Routing;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Tests.Application.Routing;

public class RoutePathfinderTests
{
    [Fact]
    public void FindShortestPath_ReturnsMeasuredShortestPath()
    {
        var originId = Guid.NewGuid();
        var middleId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var direct = Edge(originId, destinationId, 10);
        var first = Edge(originId, middleId, 3);
        var second = Edge(middleId, destinationId, 4);

        var path = RoutePathfinder.FindShortestPath(
            [direct, first, second],
            originId,
            destinationId
        );

        Assert.Equal([first.ConnectorId, second.ConnectorId], path.Select(leg => leg.ConnectorId));
    }

    [Fact]
    public void FindShortestPath_ReturnsEmpty_WhenNoMeasuredEdgeExists()
    {
        var originId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var path = RoutePathfinder.FindShortestPath([], originId, destinationId);

        Assert.Empty(path);
    }

    [Fact]
    public void FindShortestPath_IncludesMeasuredZeroDistanceConnectors()
    {
        var originId = Guid.NewGuid();
        var middleId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var indoor = Edge(originId, middleId, 0);
        var street = Edge(middleId, destinationId, 5);

        var path = RoutePathfinder.FindShortestPath([indoor, street], originId, destinationId);

        Assert.Equal([indoor.ConnectorId, street.ConnectorId], path.Select(leg => leg.ConnectorId));
    }

    [Fact]
    public void FindShortestPath_Throws_WhenMultipleMeasuredConnectorsJoinTheSameLocations()
    {
        var originId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var first = Edge(originId, destinationId, 3);
        var second = Edge(originId, destinationId, 4);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RoutePathfinder.FindShortestPath([first, second], originId, destinationId)
        );

        Assert.Contains("same ordered pair", exception.Message, StringComparison.Ordinal);
    }

    private static TravelTopologyEdge Edge(Guid originId, Guid destinationId, float distance) =>
        new(Guid.NewGuid(), Guid.NewGuid(), originId, destinationId, distance);
}
