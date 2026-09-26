using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.GameTurns.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

public sealed class PublishAmbientSceneCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private Creature _player = null!;

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PublishAmbientSceneCommandHandler _handler = null!;
    private TestGameClientEventSink _events = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<PublishAmbientSceneCommandHandler>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        var country = Builders.MakeCountry(_worldId);
        var state = Builders.MakeState(country.Id);
        var location = Builders.MakeLocation(
            _worldId,
            state.Id,
            id: _locationId,
            kind: LocationKind.Wilderness
        );
        _player = Builders.MakeCreature(_worldId, locationId: _locationId, currentHp: 1);
        _context.Worlds.Add(Builders.MakeWorld(_worldId));
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Locations.Add(location);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private PublishAmbientSceneCommand MakeCommand() =>
        new()
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            GameTime = GameClock.Epoch,
        };

    [Fact]
    public async Task Handle_PublishesStampedScene_WhenNoSceneWasPublishedYet()
    {
        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        var published = Assert.IsType<SceneUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(_player.Id, published.Scene.Player.Id);
        Assert.Equal(1, published.Stamp.Version);
    }

    [Fact]
    public async Task Handle_PublishesNothing_WhenOnlyVitalsAndTheClockChanged()
    {
        // Arrange
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);
        _events.EnqueuedEvents.Clear();
        await _context
            .Creatures.Where(creature => creature.Id == _player.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.CurrentHp, 2),
                TestContext.Current.CancellationToken
            );

        // Act
        await _handler.Handle(
            new PublishAmbientSceneCommand
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                GameTime = GameClock.Epoch + TimeSpan.FromMinutes(45),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Handle_PublishesNextVersion_WhenAPlayerVisibleChangeOccurred()
    {
        // Arrange
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);
        _events.EnqueuedEvents.Clear();
        _context.Creatures.Add(Builders.MakeCreature(_worldId, locationId: _locationId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        var published = Assert.IsType<SceneUpdatedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(2, published.Stamp.Version);
        Assert.Single(published.Scene.NearbyCreatures);
    }
}
