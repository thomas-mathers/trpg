using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Scenes.Events;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Publishing;

public sealed class PropOccupancyAnnouncerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _otherLocationId = Guid.NewGuid();

    private Creature _player = null!;
    private Seat _seat = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PropOccupancyAnnouncer _announcer = null!;
    private TestGameClientEventSink _events = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _announcer = _serviceProvider.GetRequiredService<PropOccupancyAnnouncer>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        var country = Builders.MakeCountry(_worldId);
        var state = Builders.MakeState(country.Id);
        var world = Builders.MakeWorld(_worldId);
        _player = Builders.MakeCreature(_worldId, locationId: _locationId);
        _seat = Builders.MakeSeat(_worldId, _locationId);
        world.PlayerId = _player.Id;
        _context.Worlds.Add(world);
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Locations.AddRange(
            Builders.MakeLocation(
                _worldId,
                state.Id,
                id: _locationId,
                kind: LocationKind.Wilderness
            ),
            Builders.MakeLocation(
                _worldId,
                state.Id,
                id: _otherLocationId,
                kind: LocationKind.Wilderness
            )
        );
        _context.Creatures.Add(_player);
        _context.Props.Add(_seat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Announce_PublishesNothing_WhenNoSceneWasPublishedYet()
    {
        // Arrange
        await SetOccupant(Guid.NewGuid());

        // Act
        await _announcer.Announce(_worldId, [_locationId], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Announce_PublishesTheSeatAsOccupied_WhenACreatureSat()
    {
        // Arrange
        await PublishBaselineScene();
        await SetOccupant(Guid.NewGuid());

        // Act
        await _announcer.Announce(_worldId, [_locationId], TestContext.Current.CancellationToken);

        // Assert
        var changed = Assert.IsType<PropOccupancyChangedEvent>(
            Assert.Single(_events.EnqueuedEvents)
        );
        Assert.Equal(new PropOccupancy(_seat.Id, true, false), Assert.Single(changed.Props));
    }

    [Fact]
    public async Task Announce_PublishesTheSeatAsFree_WhenTheOccupantStoodUp()
    {
        // Arrange
        await SetOccupant(Guid.NewGuid());
        await PublishBaselineScene();
        await SetOccupant(null);

        // Act
        await _announcer.Announce(_worldId, [_locationId], TestContext.Current.CancellationToken);

        // Assert
        var changed = Assert.IsType<PropOccupancyChangedEvent>(
            Assert.Single(_events.EnqueuedEvents)
        );
        Assert.Equal(new PropOccupancy(_seat.Id, false, false), Assert.Single(changed.Props));
    }

    [Fact]
    public async Task Announce_PublishesNothing_WhenOccupancyIsUnchanged()
    {
        // Arrange
        await PublishBaselineScene();

        // Act
        await _announcer.Announce(_worldId, [_locationId], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Announce_PublishesNothing_WhenTheChangedLocationIsNotThePlayers()
    {
        // Arrange
        await PublishBaselineScene();
        await SetOccupant(Guid.NewGuid());

        // Act
        await _announcer.Announce(
            _worldId,
            [_otherLocationId],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    private Task<int> SetOccupant(Guid? occupantId) =>
        _context
            .Props.OfType<Seat>()
            .Where(prop => prop.Id == _seat.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(prop => prop.OccupantId, occupantId),
                TestContext.Current.CancellationToken
            );

    private async Task PublishBaselineScene()
    {
        await _serviceProvider.PublishBaselineScene(
            _worldId,
            _player.Id,
            GameClock.Epoch,
            TestContext.Current.CancellationToken
        );
        _events.EnqueuedEvents.Clear();
    }
}
