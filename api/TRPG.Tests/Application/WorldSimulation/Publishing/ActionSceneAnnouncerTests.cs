using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.WorldSimulation.Events;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Publishing;

public sealed class ActionSceneAnnouncerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _otherLocationId = Guid.NewGuid();

    private Creature _player = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private IActionSceneAnnouncer _announcer = null!;
    private TestGameClientEventSink _events = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _announcer = _serviceProvider.GetRequiredService<IActionSceneAnnouncer>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        var country = Builders.MakeCountry(_worldId);
        var state = Builders.MakeState(country.Id);
        var world = Builders.MakeWorld(_worldId);
        _player = Builders.MakeCreature(_worldId, locationId: _locationId);
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
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AnnounceActionChanges_PublishesAnUpdateOfThePlayer_WhenThePlayerChanged()
    {
        // Arrange
        await PublishBaselineScene();
        await SetCreatureLevel(_player.Id, _player.Level + 1);

        // Act
        await _announcer.AnnounceActionChanges(
            _worldId,
            _player.Id,
            GameClock.Epoch,
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = Assert.IsType<CreaturesUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(_player.Level + 1, Assert.Single(updated.Creatures).Level);
    }

    [Fact]
    public async Task AnnounceActionChanges_PublishesAnUpdateOfANearbyCreature_WhenItWasHurt()
    {
        // Arrange
        var resident = await SeedCreatureAt(_locationId);
        await PublishBaselineScene();
        await _context
            .Creatures.Where(creature => creature.Id == resident.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.CurrentHp, 1),
                TestContext.Current.CancellationToken
            );

        // Act
        await _announcer.AnnounceActionChanges(
            _worldId,
            _player.Id,
            GameClock.Epoch,
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = Assert.IsType<CreaturesUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(1, Assert.Single(updated.Creatures, c => c.Id == resident.Id).CurrentHp);
    }

    [Fact]
    public async Task AnnounceActionChanges_PublishesNoMembershipOrSceneChange_WhenNothingChanged()
    {
        // Arrange
        await SeedCreatureAt(_locationId);
        await PublishBaselineScene();

        // Act
        await _announcer.AnnounceActionChanges(
            _worldId,
            _player.Id,
            GameClock.Epoch,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(
            _events.EnqueuedEvents,
            published =>
                published is CreaturesArrivedEvent or CreaturesLeftEvent or SceneUpdatedEvent
        );
    }

    [Fact]
    public async Task AnnounceActionChanges_PublishesTheNewScene_WhenThePlayerLeftThePublishedLocation()
    {
        // Arrange
        await PublishBaselineScene();
        await MovePlayerTo(_otherLocationId);

        // Act
        await _announcer.AnnounceActionChanges(
            _worldId,
            _player.Id,
            GameClock.Epoch,
            TestContext.Current.CancellationToken
        );

        // Assert
        var scene = Assert.IsType<SceneUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(_otherLocationId, scene.Scene.LocationId);
    }

    [Fact]
    public async Task AnnounceTimeAdvance_PublishesAClockReanchor_WhenThePlayerStayed()
    {
        // Arrange
        await PublishBaselineScene();

        // Act
        await _announcer.AnnounceTimeAdvance(
            _worldId,
            _player.Id,
            GameClock.Epoch + TimeSpan.FromHours(1),
            TestContext.Current.CancellationToken
        );

        // Assert
        var reanchored = Assert.Single(_events.EnqueuedEvents.OfType<ClockReanchoredEvent>());
        Assert.Equal(_locationId, reanchored.LocationId);
    }

    [Fact]
    public async Task AnnounceTimeAdvance_PublishesOnlyTheNewScene_WhenThePlayerLeftThePublishedLocation()
    {
        // Arrange
        await PublishBaselineScene();
        await MovePlayerTo(_otherLocationId);

        // Act
        await _announcer.AnnounceTimeAdvance(
            _worldId,
            _player.Id,
            GameClock.Epoch + TimeSpan.FromHours(1),
            TestContext.Current.CancellationToken
        );

        // Assert
        var scene = Assert.IsType<SceneUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(_otherLocationId, scene.Scene.LocationId);
    }

    [Fact]
    public async Task AnnounceTimeAdvance_PublishesOnlyTheScene_WhenNoSceneWasPublishedYet()
    {
        // Act
        await _announcer.AnnounceTimeAdvance(
            _worldId,
            _player.Id,
            GameClock.Epoch + TimeSpan.FromHours(1),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.IsType<SceneUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
    }

    private Task SetCreatureLevel(Guid creatureId, int level) =>
        _context
            .Creatures.Where(creature => creature.Id == creatureId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.Level, level),
                TestContext.Current.CancellationToken
            );

    private Task MovePlayerTo(Guid locationId) =>
        _context
            .Creatures.Where(creature => creature.Id == _player.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.LocationId, locationId),
                TestContext.Current.CancellationToken
            );

    private async Task<Creature> SeedCreatureAt(Guid locationId)
    {
        var creature = Builders.MakeCreature(_worldId, locationId: locationId);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

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
