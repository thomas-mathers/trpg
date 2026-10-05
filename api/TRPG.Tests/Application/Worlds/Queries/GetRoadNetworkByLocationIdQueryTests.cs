using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

public sealed class GetRoadNetworkByLocationIdQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private GetRoadNetworkByLocationIdQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetRoadNetworkByLocationIdQueryHandler(_context);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheNodesAndEdgesOfTheLocation()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var node = MakeNode(worldId, locationId);
        var edge = MakeEdge(worldId, locationId, node.Id);
        _context.RoadNodes.AddRange(node, MakeNode(worldId, Guid.NewGuid()));
        _context.RoadEdges.AddRange(edge, MakeEdge(worldId, Guid.NewGuid(), Guid.NewGuid()));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var network = await _handler.Handle(
            new GetRoadNetworkByLocationIdQuery { LocationId = locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(node.Id, Assert.Single(network.Nodes).Id);
        Assert.Equal(edge.Id, Assert.Single(network.Edges).Id);
    }

    [Fact]
    public async Task Handle_ReturnsTheEdgeWaypointsInOrder()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var edge = MakeEdge(
            worldId,
            locationId,
            Guid.NewGuid(),
            new Polyline { Points = [new Point(1, 2), new Point(3, 4)] }
        );
        _context.RoadEdges.Add(edge);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var network = await _handler.Handle(
            new GetRoadNetworkByLocationIdQuery { LocationId = locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var waypoints = Assert.Single(network.Edges).Waypoints.Points;
        Assert.Equal([1.0, 3.0], waypoints.Select(point => point.X));
        Assert.Equal([2.0, 4.0], waypoints.Select(point => point.Y));
    }

    private static RoadNode MakeNode(Guid worldId, Guid locationId) =>
        new()
        {
            WorldId = worldId,
            LocationId = locationId,
            Kind = RoadNodeKind.Junction,
        };

    private static RoadEdge MakeEdge(
        Guid worldId,
        Guid locationId,
        Guid nodeId,
        Polyline? waypoints = null
    ) =>
        new()
        {
            WorldId = worldId,
            LocationId = locationId,
            FromNodeId = nodeId,
            ToNodeId = Guid.NewGuid(),
            Class = RoadClass.Street,
            Waypoints = waypoints ?? new Polyline(),
        };
}
