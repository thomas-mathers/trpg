using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

public sealed class GetTravelGraphQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private MemoryCache _cache = null!;
    private GetTravelGraphQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _handler = new GetTravelGraphQueryHandler(_context, _cache);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cache.Dispose();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheRequestedWorldsGraph()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var otherWorldId = Guid.NewGuid();
        var topology = new WalkableTopology(worldId, walkMetersPerLocation: 3);
        topology.ConnectBothWays(Guid.NewGuid(), Guid.NewGuid());
        var otherTopology = new WalkableTopology(otherWorldId, walkMetersPerLocation: 5);
        otherTopology.ConnectBothWays(Guid.NewGuid(), Guid.NewGuid());
        await Seed(topology, otherTopology);

        // Act
        var graph = await _handler.Handle(
            new GetTravelGraphQuery { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.All(
            topology.LocationConnectors,
            connector =>
                Assert.Contains(
                    connector.Id,
                    graph.ConnectorsFrom(connector.OriginLocationId).Select(item => item.Id)
                )
        );
        Assert.All(
            topology.TravelNodes,
            node => Assert.Contains(node.Id, graph.NodeIdsAt(node.LocationId))
        );
    }

    [Fact]
    public async Task Handle_ServesTheCachedGraph_WhenTheWorldWasAlreadyLoaded()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var topology = new WalkableTopology(worldId, walkMetersPerLocation: 3);
        topology.ConnectBothWays(Guid.NewGuid(), Guid.NewGuid());
        await Seed(topology);
        var first = await _handler.Handle(
            new GetTravelGraphQuery { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Act
        var second = await _handler.Handle(
            new GetTravelGraphQuery { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Same(first, second);
    }

    private async Task Seed(params WalkableTopology[] topologies)
    {
        foreach (var topology in topologies)
        {
            _context.LocationConnectors.AddRange(topology.LocationConnectors);
            _context.PointConnectors.AddRange(topology.BuildPointConnectors());
            _context.TravelNodes.AddRange(topology.TravelNodes);
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
