using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns;
using TRPG.Application.WorldSimulation.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class RefreshSceneHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RefreshSceneHandler _handler = null!;
    private GameTurnSession _session = null!;
    private TestGameClientEventSink _events = null!;
    private Guid _locationId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .WithSimulationPump(db.ConnectionString)
            .AddScoped<IGameClientEventDispatcher, NoOpGameClientEventDispatcher>()
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<RefreshSceneHandler>();
        _serviceProvider.GetRequiredService<SimulationPump>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: _worldId);
        var location = Builders.MakeLocation(_worldId, state.Id);
        _locationId = location.Id;
        var player = Builders.MakeCreature(_worldId, locationId: location.Id);
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);

        _context.Worlds.Add(Builders.MakeWorld(_worldId, GameClock.Epoch));
        _context.States.Add(state);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        _context.GameSessions.Add(gameSession);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PublishesASnapshotOfThePlayersLocation()
    {
        // Act
        await _handler.Handle(_session, TestContext.Current.CancellationToken);

        // Assert
        var snapshot = Assert.Single(_events.EnqueuedEvents.OfType<SceneUpdatedEvent>());
        Assert.Equal(_locationId, snapshot.Scene.LocationId);
    }
}
