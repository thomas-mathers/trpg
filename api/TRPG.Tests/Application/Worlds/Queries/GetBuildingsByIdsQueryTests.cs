using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetBuildingsByIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetBuildingsByIdsQueryHandler _handler = null!;
    private readonly Building _first = Builders.MakeBuilding(worldId: WorldId);
    private readonly Building _second = Builders.MakeBuilding(worldId: WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetBuildingsByIdsQueryHandler>();

        _context.Buildings.AddRange(_first, _second);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsRequestedBuildingsKeyedById()
    {
        // Act
        var result = await _handler.Handle(
            new GetBuildingsByIdsQuery { Ids = [_first.Id, Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var building = Assert.Single(result);
        Assert.Equal(_first.Id, building.Key);
        Assert.Equal(_first.Name, building.Value.Name);
    }
}
