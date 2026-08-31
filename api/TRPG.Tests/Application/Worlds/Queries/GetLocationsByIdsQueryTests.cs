using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetLocationsByIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetLocationsByIdsQueryHandler _handler = null!;
    private readonly Location _first = Builders.MakeLocation(WorldId);
    private readonly Location _second = Builders.MakeLocation(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetLocationsByIdsQueryHandler>();

        _context.Locations.AddRange(_first, _second);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsRequestedLocationsKeyedById()
    {
        // Act
        var result = await _handler.Handle(
            new GetLocationsByIdsQuery { Ids = [_first.Id, Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var location = Assert.Single(result);
        Assert.Equal(_first.Id, location.Key);
        Assert.Equal(_first.StateId, location.Value.StateId);
    }
}
