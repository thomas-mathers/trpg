using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class DropWorldTravelGraphCacheTests(DatabaseFixture db)
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
    public async Task Handle_EvictsTheDroppedWorldsTravelGraph()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        _cache.Set(GetTravelGraphQueryHandler.CacheKey(worldId), new object());

        // Act
        await _handler.Handle(
            new DropWorldCommand { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(_cache.TryGetValue(GetTravelGraphQueryHandler.CacheKey(worldId), out _));
    }

    [Fact]
    public async Task Handle_KeepsOtherWorldsTravelGraph()
    {
        // Arrange
        var otherWorldId = Guid.NewGuid();
        _cache.Set(GetTravelGraphQueryHandler.CacheKey(otherWorldId), new object());

        // Act
        await _handler.Handle(
            new DropWorldCommand { WorldId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(_cache.TryGetValue(GetTravelGraphQueryHandler.CacheKey(otherWorldId), out _));
    }
}
