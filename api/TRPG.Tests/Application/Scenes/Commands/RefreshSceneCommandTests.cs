using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Scenes.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Commands;

[Collection("Database")]
public sealed class RefreshSceneCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RefreshSceneCommandHandler _handler = null!;
    private Location _location = null!;
    private Creature _player = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<RefreshSceneCommandHandler>();

        _location = Builders.MakeLocation(WorldId, _stateId);
        _player = Builders.MakeCreature(WorldId, locationId: _location.Id);
        _session = Builders.MakeGameSession(WorldId, _player.Id);
        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);

        _context.Locations.Add(_location);
        _context.Creatures.Add(_player);
        _context.States.Add(state);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheScene_ForThePlayersCurrentLocation()
    {
        // Act
        var result = await _handler.Handle(
            new RefreshSceneCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                Playtime = _session.Playtime,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(_player.Id, result.Scene.Player.Id);
    }

    [Fact]
    public async Task Handle_ReturnsRefreshedTrue_OnTheFirstCallForALocation()
    {
        // Act
        var result = await _handler.Handle(
            new RefreshSceneCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                Playtime = _session.Playtime,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.Refreshed);
    }

    [Fact]
    public async Task Handle_ReturnsRefreshedFalse_OnASecondCallWithinTheSameHour()
    {
        // Arrange
        await _handler.Handle(
            new RefreshSceneCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                Playtime = _session.Playtime,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _handler.Handle(
            new RefreshSceneCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                Playtime = _session.Playtime,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.Refreshed);
    }
}
