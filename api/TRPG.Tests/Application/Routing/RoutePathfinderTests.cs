using TRPG.Application.Routing;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Routing;

public class RoutePathfinderTests
{
    [Fact]
    public void FindShortestPath_ReturnsMeasuredShortestPath()
    {
        var originId = Guid.NewGuid();
        var middleId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var direct = Connector(originId, destinationId);
        var first = Connector(originId, middleId);
        var second = Connector(middleId, destinationId);

        var path = RoutePathfinder.FindShortestPath(
            [direct, first, second],
            [Travel(direct, 10), Travel(first, 3), Travel(second, 4)],
            originId,
            destinationId
        );

        Assert.Equal([first.Id, second.Id], path.Select(leg => leg.ConnectorId));
    }

    [Fact]
    public void FindShortestPath_IgnoresConnectorsWithoutMeasuredDistance()
    {
        var originId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var connector = Connector(originId, destinationId);

        var path = RoutePathfinder.FindShortestPath([connector], [], originId, destinationId);

        Assert.Empty(path);
    }

    [Fact]
    public void FindShortestPath_Throws_WhenMultipleMeasuredConnectorsJoinTheSameLocations()
    {
        var originId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var first = Connector(originId, destinationId);
        var second = Connector(originId, destinationId);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RoutePathfinder.FindShortestPath(
                [first, second],
                [Travel(first, 3), Travel(second, 4)],
                originId,
                destinationId
            )
        );

        Assert.Contains("same ordered pair", exception.Message, StringComparison.Ordinal);
    }

    private static LocationConnector Connector(Guid originId, Guid destinationId) =>
        new()
        {
            WorldId = Guid.NewGuid(),
            OriginLocationId = originId,
            DestinationLocationId = destinationId,
            DestinationLabel = "Destination",
        };

    private static TravelConnector Travel(LocationConnector connector, float distance) =>
        new()
        {
            WorldId = connector.WorldId,
            ConnectorId = connector.Id,
            Distance = distance,
        };
}
