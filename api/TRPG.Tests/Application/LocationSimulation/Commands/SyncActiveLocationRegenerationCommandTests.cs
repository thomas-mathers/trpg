using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Events;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SyncActiveLocationRegenerationCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant OneTickIn = GameClock.Epoch + TimeSpan.FromSeconds(5);

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncActiveLocationRegenerationCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<SyncActiveLocationRegenerationCommandHandler>();

        _player = Builders.MakeCreature(_worldId, locationId: _locationId, currentHp: 1);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private SyncActiveLocationRegenerationCommand MakeCommand(GameInstant? gameTime = null) =>
        new()
        {
            WorldId = _worldId,
            GameTime = gameTime ?? OneTickIn,
            Players = [new ActiveLocationPlayer(_locationId, _player.Id, PlayerLevel: 1)],
        };

    private async Task<int> ReadCurrentHp(Guid creatureId)
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Creatures.Where(creature => creature.Id == creatureId)
            .Select(creature => creature.CurrentHp)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_RegeneratesPlayerAndPublishesVitals_WhenPlayerIsBelowMaximum()
    {
        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        var published = Assert.Single(
            _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents
        );
        var vitalsChanged = Assert.IsType<PlayerVitalsChangedEvent>(published);
        Assert.Equal(_player.Id, vitalsChanged.Vitals.CreatureId);
        Assert.Equal(OneTickIn, vitalsChanged.GameTime);
        Assert.Equal(await ReadCurrentHp(_player.Id), vitalsChanged.Vitals.CurrentHp);
        Assert.True(vitalsChanged.Vitals.CurrentHp > 1);
    }

    [Fact]
    public async Task Handle_RegeneratesNonPlayerWithoutPublishing_WhenNearbyCreatureIsBelowMaximum()
    {
        // Arrange
        var bystander = Builders.MakeCreature(_worldId, locationId: _locationId, currentHp: 1);
        _context.Creatures.Add(bystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(await ReadCurrentHp(bystander.Id) > 1);
        var published = _serviceProvider
            .GetRequiredService<TestGameClientEventSink>()
            .EnqueuedEvents.OfType<PlayerVitalsChangedEvent>();
        Assert.Equal(_player.Id, Assert.Single(published).Vitals.CreatureId);
    }

    [Fact]
    public async Task Handle_SuppressesRegeneration_WhenCreatureIsInAnActiveFight()
    {
        // Arrange
        _context.Encounters.Add(Builders.MakeFight(_worldId, _player.Id, [_player.Id]));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, await ReadCurrentHp(_player.Id));
        Assert.Empty(_serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents);
    }

    [Fact]
    public async Task Handle_PublishesNothing_WhenLessThanOneTickHasElapsed()
    {
        // Act
        await _handler.Handle(
            MakeCommand(GameClock.Epoch + TimeSpan.FromSeconds(4)),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents);
    }
}
