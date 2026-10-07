using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

public sealed class GetNearestReachableLocationQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetNearestReachableLocationQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetNearestReachableLocationQueryHandler>();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheCandidate_WhenItSharesTheSameAnchorAsTheFromLocation()
    {
        // Arrange
        var anchor = Guid.NewGuid();
        var fromLocation = Builders.MakeLocation(WorldId, coarseAnchorLocationId: anchor);
        var candidate = Builders.MakeLocation(WorldId, coarseAnchorLocationId: anchor);
        _context.Locations.AddRange(fromLocation, candidate);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetNearestReachableLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = fromLocation.Id,
            CandidateLocationIds = [candidate.Id],
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(candidate.Id, result);
    }

    [Fact]
    public async Task Handle_PicksTheCheaperCandidate_WhenMultipleAreReachable()
    {
        // Arrange
        var fromAnchor = Guid.NewGuid();
        var nearAnchor = Guid.NewGuid();
        var farAnchor = Guid.NewGuid();
        var fromLocation = Builders.MakeLocation(WorldId, coarseAnchorLocationId: fromAnchor);
        var nearCandidate = Builders.MakeLocation(WorldId, coarseAnchorLocationId: nearAnchor);
        var farCandidate = Builders.MakeLocation(WorldId, coarseAnchorLocationId: farAnchor);
        _context.Locations.AddRange(fromLocation, nearCandidate, farCandidate);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var viaAnchor = Guid.NewGuid();
        var topology = new WalkableTopology(WorldId, walkMetersPerLocation: 10);
        topology.ConnectBothWays(fromAnchor, nearAnchor);
        topology.ConnectBothWays(fromAnchor, viaAnchor);
        topology.ConnectBothWays(viaAnchor, farAnchor);
        await Seed(topology);

        var query = new GetNearestReachableLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = fromLocation.Id,
            CandidateLocationIds = [nearCandidate.Id, farCandidate.Id],
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nearCandidate.Id, result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoCandidateIsReachable()
    {
        // Arrange
        var fromLocation = Builders.MakeLocation(WorldId, coarseAnchorLocationId: Guid.NewGuid());
        var candidate = Builders.MakeLocation(WorldId, coarseAnchorLocationId: Guid.NewGuid());
        _context.Locations.AddRange(fromLocation, candidate);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetNearestReachableLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = fromLocation.Id,
            CandidateLocationIds = [candidate.Id],
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoCandidatesAreGiven()
    {
        // Arrange
        var fromLocation = Builders.MakeLocation(WorldId);
        _context.Locations.Add(fromLocation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetNearestReachableLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = fromLocation.Id,
            CandidateLocationIds = [],
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    private async Task Seed(WalkableTopology topology)
    {
        _context.LocationConnectors.AddRange(topology.LocationConnectors);
        _context.PointConnectors.AddRange(topology.BuildPointConnectors());
        _context.TravelNodes.AddRange(topology.TravelNodes);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
