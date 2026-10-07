using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

public sealed class GetPointNetworkByLocationIdQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private GetPointNetworkByLocationIdQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetPointNetworkByLocationIdQueryHandler(_context);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheNodesAndConnectorsOfTheLocation()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var node = Builders.MakeTravelNode(locationId);
        var connector = Builders.MakePointConnector(locationId, node.Id, Guid.NewGuid(), 5);
        _context.TravelNodes.AddRange(node, Builders.MakeTravelNode(Guid.NewGuid()));
        _context.PointConnectors.AddRange(
            connector,
            Builders.MakePointConnector(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var network = await _handler.Handle(
            new GetPointNetworkByLocationIdQuery { LocationId = locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(node.Id, Assert.Single(network.Nodes).Id);
        Assert.Equal(connector.Id, Assert.Single(network.Connectors).Id);
    }

    [Fact]
    public async Task Handle_ReturnsTheConnectorWaypointsInOrder()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var connector = Builders.MakePointConnector(locationId, Guid.NewGuid(), Guid.NewGuid(), 5);
        var waypointed = new PointConnector
        {
            WorldId = connector.WorldId,
            LocationId = locationId,
            OriginNodeId = connector.OriginNodeId,
            DestinationNodeId = connector.DestinationNodeId,
            Waypoints = new Polyline { Points = [new Point(1, 2), new Point(3, 4)] },
        };
        _context.PointConnectors.Add(waypointed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var network = await _handler.Handle(
            new GetPointNetworkByLocationIdQuery { LocationId = locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var waypoints = Assert.Single(network.Connectors).Waypoints.Points;
        Assert.Equal([1.0, 3.0], waypoints.Select(point => point.X));
        Assert.Equal([2.0, 4.0], waypoints.Select(point => point.Y));
    }

    [Fact]
    public async Task Handle_MarksTheNodesWhereLocationConnectorsEnterOrLeaveAsPorts()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var door = Builders.MakeLocationConnector(locationId);
        var exitNode = Builders.MakeExitNode(door);
        var interiorNode = Builders.MakeTravelNode(locationId);
        _context.LocationConnectors.Add(door);
        _context.TravelNodes.AddRange(exitNode, Builders.MakeArrivalNode(door), interiorNode);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var network = await _handler.Handle(
            new GetPointNetworkByLocationIdQuery { LocationId = locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([exitNode.Id], network.PortNodeIds);
    }
}
