using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Commands;

public sealed class PublishSceneCreatureChangesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _otherLocationId = Guid.NewGuid();

    private Creature _player = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PublishSceneCreatureChangesCommandHandler _handler = null!;
    private PublishAmbientSceneCommandHandler _publishAmbientScene = null!;
    private TestGameClientEventSink _events = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<PublishSceneCreatureChangesCommandHandler>();
        _publishAmbientScene =
            _serviceProvider.GetRequiredService<PublishAmbientSceneCommandHandler>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        var country = Builders.MakeCountry(_worldId);
        var state = Builders.MakeState(country.Id);
        _player = Builders.MakeCreature(_worldId, locationId: _locationId);
        _context.Worlds.Add(Builders.MakeWorld(_worldId));
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
    public async Task Handle_PublishesTheFullScene_WhenNoSceneWasPublishedYet()
    {
        // Act
        await _handler.Handle(MakeCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_events.EnqueuedEvents.OfType<SceneUpdatedEvent>());
    }

    [Fact]
    public async Task Handle_PublishesOnlyAnArrival_WhenACreatureEntersThePlayersLocation()
    {
        // Arrange
        await PublishBaselineScene();
        var visitor = Builders.MakeCreature(_worldId, locationId: _locationId);
        _context.Creatures.Add(visitor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(visitor.Id), TestContext.Current.CancellationToken);

        // Assert
        var arrived = Assert.IsType<CreaturesArrivedEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(visitor.Id, Assert.Single(arrived.Creatures).Id);
    }

    [Fact]
    public async Task Handle_PublishesOnlyADeparture_WhenACreatureLeavesThePlayersLocation()
    {
        // Arrange
        var resident = Builders.MakeCreature(_worldId, locationId: _locationId);
        _context.Creatures.Add(resident);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await PublishBaselineScene();
        await _context
            .Creatures.Where(creature => creature.Id == resident.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.LocationId, _otherLocationId),
                TestContext.Current.CancellationToken
            );

        // Act
        await _handler.Handle(MakeCommand(resident.Id), TestContext.Current.CancellationToken);

        // Assert
        var left = Assert.IsType<CreaturesLeftEvent>(Assert.Single(_events.EnqueuedEvents));
        Assert.Equal(resident.Id, Assert.Single(left.CreatureIds));
    }

    [Fact]
    public async Task Handle_PublishesNothing_WhenTheCreaturesAreUnchanged()
    {
        // Arrange
        var resident = Builders.MakeCreature(_worldId, locationId: _locationId);
        _context.Creatures.Add(resident);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await PublishBaselineScene();

        // Act
        await _handler.Handle(MakeCommand(resident.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    private async Task PublishBaselineScene()
    {
        await _publishAmbientScene.Handle(
            new PublishAmbientSceneCommand
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );
        _events.EnqueuedEvents.Clear();
    }

    private PublishSceneCreatureChangesCommand MakeCommand(params Guid[] creatureIds) =>
        new()
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            LocationId = _locationId,
            GameTime = GameClock.Epoch,
            CreatureIds = creatureIds,
        };
}
