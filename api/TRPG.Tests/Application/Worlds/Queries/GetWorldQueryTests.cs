using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetWorldQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetWorldQueryHandler _handler = null!;
    private readonly World _world = Builders.MakeWorld();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetWorldQueryHandler(_context);

        _context.Worlds.Add(_world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _handler.Handle(
            new GetWorldQuery { WorldId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsWorld_WhenExists()
    {
        // Act
        var result = await _handler.Handle(
            new GetWorldQuery { WorldId = _world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_world.Id, result.Id);
    }
}
