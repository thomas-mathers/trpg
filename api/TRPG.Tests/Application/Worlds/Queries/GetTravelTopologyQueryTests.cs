using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

public sealed class GetTravelTopologyQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private MemoryCache _cache = null!;
    private GetTravelTopologyQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _handler = new GetTravelTopologyQueryHandler(_context, _cache);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cache.Dispose();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheRequestedWorldsConnectorsAndNodes()
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
        var result = await _handler.Handle(
            new GetTravelTopologyQuery { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            topology.LocationConnectors.Select(connector => connector.Id).Order(),
            result.LocationConnectors.Select(connector => connector.Id).Order()
        );
        Assert.Equal(
            topology.TravelNodes.Select(node => node.Id).Order(),
            result.Nodes.Select(node => node.Id).Order()
        );
        Assert.All(result.PointConnectors, connector => Assert.Equal(3, connector.Distance));
    }

    [Fact]
    public async Task Handle_ServesTheCachedTopology_WhenTheWorldWasAlreadyLoaded()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var topology = new WalkableTopology(worldId, walkMetersPerLocation: 3);
        topology.ConnectBothWays(Guid.NewGuid(), Guid.NewGuid());
        await Seed(topology);
        var first = await _handler.Handle(
            new GetTravelTopologyQuery { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Act
        var second = await _handler.Handle(
            new GetTravelTopologyQuery { WorldId = worldId },
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
