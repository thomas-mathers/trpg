using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetNearestReachableLocationQueryTests(DatabaseFixture db) : IAsyncLifetime
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
        await SeedTravelConnector(fromAnchor, nearAnchor, distance: 1);
        await SeedTravelConnector(fromAnchor, farAnchor, distance: 5);

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

    private async Task SeedTravelConnector(
        Guid originLocationId,
        Guid destinationLocationId,
        float distance
    )
    {
        var outbound = Builders.MakeLocationConnector(
            originLocationId,
            destinationLocationId,
            WorldId
        );
        var inbound = Builders.MakeLocationConnector(
            destinationLocationId,
            originLocationId,
            WorldId
        );
        _context.LocationConnectors.AddRange(outbound, inbound);
        _context.TravelConnectors.AddRange(
            Builders.MakeTravelConnector(outbound.Id, distance, worldId: WorldId),
            Builders.MakeTravelConnector(inbound.Id, distance, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
