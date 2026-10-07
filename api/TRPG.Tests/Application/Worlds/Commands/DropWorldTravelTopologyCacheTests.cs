using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class DropWorldTravelTopologyCacheTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private DropWorldCommandHandler _handler = null!;
    private IMemoryCache _cache = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<DropWorldCommandHandler>();
        _cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Handle_EvictsTheDroppedWorldsTravelTopology()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        _cache.Set(GetTravelTopologyQueryHandler.CacheKey(worldId), new object());

        // Act
        await _handler.Handle(
            new DropWorldCommand { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(_cache.TryGetValue(GetTravelTopologyQueryHandler.CacheKey(worldId), out _));
    }

    [Fact]
    public async Task Handle_KeepsOtherWorldsTravelTopology()
    {
        // Arrange
        var otherWorldId = Guid.NewGuid();
        _cache.Set(GetTravelTopologyQueryHandler.CacheKey(otherWorldId), new object());

        // Act
        await _handler.Handle(
            new DropWorldCommand { WorldId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(
            _cache.TryGetValue(GetTravelTopologyQueryHandler.CacheKey(otherWorldId), out _)
        );
    }
}
