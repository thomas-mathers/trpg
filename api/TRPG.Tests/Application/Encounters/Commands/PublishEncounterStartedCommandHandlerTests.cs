using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class PublishEncounterStartedCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PublishEncounterStartedCommandHandler _handler = null!;
    private TestGameClientEventSink _eventSink = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<PublishEncounterStartedCommandHandler>();
        _eventSink = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_EnqueuesHostileEncounterStartedEvent_WhenEncounterIsHostile()
    {
        // Arrange
        var encounter = Builders.MakeHostileEncounter(WorldId, _player.Id, _player.LocationId);

        // Act
        await _handler.Handle(
            new PublishEncounterStartedCommand { PlayerId = _player.Id, Encounter = encounter },
            TestContext.Current.CancellationToken
        );

        // Assert
        var startedEvent = Assert.Single(
            _eventSink.EnqueuedEvents.OfType<HostileEncounterStartedEvent>()
        );
        Assert.Same(encounter, startedEvent.Encounter);
    }

    [Fact]
    public async Task Handle_MarksTheFineAffordable_WhenGuardEncounterAndPlayerHasEnoughGold()
    {
        // Arrange
        var gold = Builders.MakeGold(WorldId, quantity: 10_000);
        gold.Ownership.OwnerId = _player.Id;
        gold.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var encounter = Builders.MakeGuardEncounter(
            WorldId,
            _player.Id,
            _player.LocationId,
            Guid.NewGuid(),
            fineAmount: 100
        );

        // Act
        await _handler.Handle(
            new PublishEncounterStartedCommand { PlayerId = _player.Id, Encounter = encounter },
            TestContext.Current.CancellationToken
        );

        // Assert
        var startedEvent = Assert.Single(
            _eventSink.EnqueuedEvents.OfType<GuardEncounterStartedEvent>()
        );
        Assert.True(startedEvent.CanAffordFine);
    }

    [Fact]
    public async Task Handle_MarksTheFineUnaffordable_WhenGuardEncounterAndPlayerLacksGold()
    {
        // Arrange
        var encounter = Builders.MakeGuardEncounter(
            WorldId,
            _player.Id,
            _player.LocationId,
            Guid.NewGuid(),
            fineAmount: 100
        );

        // Act
        await _handler.Handle(
            new PublishEncounterStartedCommand { PlayerId = _player.Id, Encounter = encounter },
            TestContext.Current.CancellationToken
        );

        // Assert
        var startedEvent = Assert.Single(
            _eventSink.EnqueuedEvents.OfType<GuardEncounterStartedEvent>()
        );
        Assert.False(startedEvent.CanAffordFine);
    }

    [Fact]
    public async Task Handle_EnqueuesTheftEncounterStartedEvent_WhenEncounterIsTheft()
    {
        // Arrange
        var encounter = new TheftEncounter
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = _player.LocationId,
            ConfrontingName = "Shopkeeper",
        };

        // Act
        await _handler.Handle(
            new PublishEncounterStartedCommand { PlayerId = _player.Id, Encounter = encounter },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Single(_eventSink.EnqueuedEvents.OfType<TheftEncounterStartedEvent>());
    }

    [Fact]
    public async Task Handle_EnqueuesSuspicionEncounterStartedEvent_WhenEncounterIsSuspicion()
    {
        // Arrange
        var encounter = Builders.MakeSuspicionEncounter(
            WorldId,
            _player.Id,
            _player.LocationId,
            Guid.NewGuid()
        );

        // Act
        await _handler.Handle(
            new PublishEncounterStartedCommand { PlayerId = _player.Id, Encounter = encounter },
            TestContext.Current.CancellationToken
        );

        // Assert
        var startedEvent = Assert.Single(
            _eventSink.EnqueuedEvents.OfType<SuspicionEncounterStartedEvent>()
        );
        Assert.Same(encounter, startedEvent.Encounter);
    }

    [Fact]
    public async Task Handle_EnqueuesNothing_WhenThereIsNoEncounter()
    {
        // Act
        await _handler.Handle(
            new PublishEncounterStartedCommand { PlayerId = _player.Id, Encounter = null },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_eventSink.EnqueuedEvents);
    }
}
