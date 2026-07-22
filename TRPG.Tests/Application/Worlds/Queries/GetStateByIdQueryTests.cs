using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetStateByIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetStateByIdQueryHandler _handler = null!;
    private State _state = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetStateByIdQueryHandler(
            _context,
            new MemoryCache(new MemoryCacheOptions())
        );

        var world = Builders.MakeWorld();
        var country = Builders.MakeCountry(world.Id);
        _state = Builders.MakeState(country.Id);
        _context.Worlds.Add(world);
        _context.Countries.Add(country);
        _context.States.Add(_state);
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
            new GetStateByIdQuery { Id = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsState_WhenExists()
    {
        // Act
        var result = await _handler.Handle(
            new GetStateByIdQuery { Id = _state.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_state.Id, result.Id);
    }
}
