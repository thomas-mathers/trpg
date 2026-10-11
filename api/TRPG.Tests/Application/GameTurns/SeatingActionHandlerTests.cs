using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns;
using TRPG.Application.Scenes.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class SeatingActionHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SitDownActionHandler _sitDown = null!;
    private StandUpActionHandler _standUp = null!;
    private GameTurnSession _session = null!;
    private TestGameClientEventSink _events = null!;
    private Seat _seat = null!;
    private Guid _locationId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .WithSimulationPump(db.ConnectionString)
            .AddScoped<IGameClientEventDispatcher, NoOpGameClientEventDispatcher>()
            .BuildServiceProvider();
        _sitDown = _serviceProvider.GetRequiredService<SitDownActionHandler>();
        _standUp = _serviceProvider.GetRequiredService<StandUpActionHandler>();
        _serviceProvider.GetRequiredService<SimulationPump>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: _worldId);
        var location = Builders.MakeLocation(_worldId, state.Id);
        _locationId = location.Id;
        var player = Builders.MakeCreature(_worldId, locationId: location.Id, x: 1, y: 2);
        _seat = Builders.MakeSeat(_worldId, location.Id, x: 4.5, y: 7.25, angle: 1.5);
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);

        _context.Worlds.Add(Builders.MakeWorld(_worldId, GameClock.Epoch));
        _context.States.Add(state);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        _context.Props.Add(_seat);
        _context.GameSessions.Add(gameSession);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await PublishBaselineScene();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Sit_AnnouncesOnlyTheSeatingChange_WhenThePlayerSits()
    {
        // Act
        await _sitDown.Handle(_session, _seat.Id, TestContext.Current.CancellationToken);

        // Assert
        var seating = Assert.IsType<SeatingChangedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(_locationId, seating.LocationId);
        Assert.Equal(_session.PlayerId, seating.CreatureId);
        Assert.Equal(_seat.Id, seating.SeatId);
        Assert.Equal(CreaturePosture.Sitting, seating.Posture);
        Assert.Equal(new Placement(4.5, 7.25, 1.5), seating.Placement);
    }

    [Fact]
    public async Task Stand_AnnouncesOnlyTheSeatingChange_WhenThePlayerStands()
    {
        // Arrange
        await _sitDown.Handle(_session, _seat.Id, TestContext.Current.CancellationToken);
        _events.EnqueuedEvents.Clear();

        // Act
        await _standUp.Handle(_session, TestContext.Current.CancellationToken);

        // Assert
        var seating = Assert.IsType<SeatingChangedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(_session.PlayerId, seating.CreatureId);
        Assert.Null(seating.SeatId);
        Assert.Equal(CreaturePosture.Standing, seating.Posture);
        Assert.Equal(new Placement(1, 2, 0), seating.Placement);
    }

    private async Task PublishBaselineScene()
    {
        await _serviceProvider.PublishBaselineScene(
            _worldId,
            _session.PlayerId,
            GameClock.Epoch,
            TestContext.Current.CancellationToken
        );
        _events.EnqueuedEvents.Clear();
    }

    private sealed class NoOpGameClientEventDispatcher : IGameClientEventDispatcher
    {
        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
