using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class WorldServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private World _world = null!;
    private WorldService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new WorldService(_context);

        _world = Builders.MakeWorld();
        _context.Worlds.Add(_world);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetWorld_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetWorld(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorld_ReturnsWorld_WhenExists() {
        // Act
        var result = await _service.GetWorld(_world.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_world.Id, result.Id);
    }

    [Fact]
    public async Task Update_PersistsPlaytime() {
        // Arrange
        _world.Playtime = TimeSpan.FromHours(5);

        // Act
        await _service.Update(_world, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Worlds.FindAsync([_world.Id], TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromHours(5), updated!.Playtime);
    }
}
