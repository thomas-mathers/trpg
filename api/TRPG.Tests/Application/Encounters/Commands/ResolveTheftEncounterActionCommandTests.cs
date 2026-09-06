using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Inventory;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class ResolveTheftEncounterActionCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _locationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveTheftEncounterActionCommandHandler _handler = null!;
    private AttemptTheftCommandHandler _attemptTheftHandler = null!;
    private Creature _confronter = null!;
    private Creature _owner = null!;
    private Creature _player = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _player = Builders.MakeCreature(WorldId, locationId: _locationId);
        _confronter = Builders.MakeCreature(WorldId, locationId: _locationId, name: "Tessa");
        _owner = Builders.MakeCreature(WorldId, locationId: _locationId, name: "Mara");

        _session = Builders.MakeGameSession(WorldId, _player.Id);
        var location = Builders.MakeLocation(WorldId, id: _locationId);

        _context.Creatures.AddRange(_player, _confronter, _owner);
        _context.GameSessions.Add(_session);
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsMonitor<TheftOptions>>(
                new TestOptionsMonitor<TheftOptions>(new TheftOptions())
            )
            .AddSingleton<IChanceRoller>(new AlwaysSuccessfulChanceRoller())
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveTheftEncounterActionCommandHandler>();
        _attemptTheftHandler = _serviceProvider.GetRequiredService<AttemptTheftCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_Apologize_ReturnsStolenItemsAndCompletesTheEncounter()
    {
        // Arrange
        var container = Builders.MakeContainer(WorldId, _locationId);
        container.OwnerCreatureId = _owner.Id;
        var item = Builders.MakeItem(WorldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = _player.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        var encounter = await SeedEncounter(
            sourceOwnerId: container.Id,
            sourceOwnerType: OwnerType.Container,
            item
        );
        _context.Props.Add(container);
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new ApologizeTheftEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var returnedItem = await verifyContext.Items.FindAsync(
            [item.Id],
            TestContext.Current.CancellationToken
        );
        var persistedEncounter = await verifyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(item => item.Id == encounter.Id, TestContext.Current.CancellationToken);
        var crime = await verifyContext
            .Crimes.OfType<TheftCrime>()
            .SingleAsync(
                item => item.Id == encounter.TheftCrimeId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(container.Id, returnedItem!.Ownership.OwnerId);
        Assert.Equal(OwnerType.Container, returnedItem.Ownership.OwnerType);
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);
        Assert.Equal(TheftCrimeOutcome.Apologized, crime.Outcome);
        Assert.Equal(TheftEncounterResolutionOutcome.Apologized, fact.Outcome);
        Assert.Equal(_owner.Name, fact.ConfrontingName);
        Assert.True(fact.ItemsReturned);
    }

    [Fact]
    public async Task Handle_Apologize_LeavesPickpocketedItemsWithTheirOwner()
    {
        var item = Builders.MakeWeapon(WorldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = _owner.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        var encounter = await SeedEncounter(
            sourceOwnerId: _owner.Id,
            sourceOwnerType: OwnerType.Creature,
            item
        );
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var fact = await _handler.Handle(
            MakeCommand(new ApologizeTheftEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var persistedItem = await verifyContext.Items.FindAsync(
            [item.Id],
            TestContext.Current.CancellationToken
        );
        var crime = await verifyContext
            .Crimes.OfType<TheftCrime>()
            .SingleAsync(
                candidate => candidate.Id == encounter.TheftCrimeId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(_owner.Id, persistedItem!.Ownership.OwnerId);
        Assert.False(fact.ItemsReturned);
        Assert.Equal(TheftCrimeOutcome.Apologized, crime.Outcome);
    }

    [Fact]
    public async Task Handle_Flee_StartsNoCombatAndMarksTheCrimeFled()
    {
        // Arrange
        var encounter = await SeedEncounter(
            sourceOwnerId: _owner.Id,
            sourceOwnerType: OwnerType.Creature,
            confrontingCreature: _confronter
        );

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new FleeTheftEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedConfronter = await verifyContext.Creatures.FindAsync(
            [_confronter.Id],
            TestContext.Current.CancellationToken
        );
        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        var crime = await verifyContext
            .Crimes.OfType<TheftCrime>()
            .SingleAsync(
                item => item.Id == encounter.TheftCrimeId,
                TestContext.Current.CancellationToken
            );
        var persistedEncounter = await verifyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(item => item.Id == encounter.Id, TestContext.Current.CancellationToken);

        Assert.Equal(CreatureState.Idle, updatedConfronter!.State);
        Assert.False(
            await verifyContext
                .Encounters.OfType<FightEncounter>()
                .AnyAsync(
                    item => item.PlayerId == _player.Id,
                    TestContext.Current.CancellationToken
                )
        );
        Assert.Equal(TheftCrimeOutcome.Fled, crime.Outcome);
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);
        Assert.Equal(TheftEncounterResolutionOutcome.Fled, fact.Outcome);
        Assert.Equal(_confronter.Name, fact.ConfrontingName);
        Assert.Equal(_locationId, updatedPlayer!.LocationId);
        Assert.Null(updatedPlayer.PreviousLocationId);
    }

    [Fact]
    public async Task Handle_Flee_MovesThePlayerToTheEncounterLocation_WhenItDiffersFromWhereTheyAre()
    {
        // Arrange — mirrors a blocked departure: the encounter's location is where the player
        // was trying to go, not where they're actually standing
        var destination = Builders.MakeLocation(WorldId);
        var destinationLocationId = destination.Id;
        _context.Locations.Add(destination);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var encounter = await SeedEncounter(
            sourceOwnerId: _owner.Id,
            sourceOwnerType: OwnerType.Creature,
            confrontingCreature: _confronter,
            locationId: destinationLocationId
        );

        // Act
        await _handler.Handle(
            MakeCommand(new FleeTheftEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var movedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(destinationLocationId, movedPlayer!.LocationId);
        Assert.Equal(_locationId, movedPlayer.PreviousLocationId);
    }

    [Fact]
    public async Task Handle_Apologize_ReturnsTheCreatedPartialStackToContainer_WhenCaughtTheftSplitsIt()
    {
        // Arrange
        var container = Builders.MakeContainer(WorldId, _locationId);
        container.OwnerCreatureId = _owner.Id;
        var sourceStack = Builders.MakeAmmunition(WorldId);
        sourceStack.Quantity = 10;
        sourceStack.Ownership.OwnerId = container.Id;
        sourceStack.Ownership.OwnerType = OwnerType.Container;
        _context.Props.Add(container);
        _context.Items.Add(sourceStack);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var theft = await _attemptTheftHandler.Handle(
            new AttemptTheftCommand
            {
                From = new ItemOwnerReference(container.Id, OwnerType.Container),
                Items = [new ItemSelection(sourceStack.Id, 3)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TheftAttemptOutcome.EncounterPending, theft.Outcome);
        await using var beforeApologyContext = db.CreateContext();
        var encounter = await beforeApologyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(
                item => item.Id == theft.EncounterId,
                TestContext.Current.CancellationToken
            );
        var selection = Assert.Single(encounter.ItemSelections);
        var originalStack = await beforeApologyContext.Items.FindAsync(
            [sourceStack.Id],
            TestContext.Current.CancellationToken
        );
        var playerStack = await beforeApologyContext.Items.FindAsync(
            [selection.ItemId],
            TestContext.Current.CancellationToken
        );

        Assert.NotEqual(sourceStack.Id, selection.ItemId);
        Assert.Equal(3, selection.Quantity);
        Assert.Equal(7, originalStack!.Quantity);
        Assert.Equal(container.Id, originalStack.Ownership.OwnerId);
        Assert.Equal(_player.Id, playerStack!.Ownership.OwnerId);
        Assert.Equal(3, playerStack.Quantity);

        await _handler.Handle(
            MakeCommand(new ApologizeTheftEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var remainingSourceStack = await verifyContext.Items.FindAsync(
            [sourceStack.Id],
            TestContext.Current.CancellationToken
        );
        var returnedPlayerStack = await verifyContext.Items.FindAsync(
            [selection.ItemId],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(7, remainingSourceStack!.Quantity);
        Assert.Equal(container.Id, remainingSourceStack.Ownership.OwnerId);
        Assert.Equal(3, returnedPlayerStack!.Quantity);
        Assert.Equal(container.Id, returnedPlayerStack.Ownership.OwnerId);
        Assert.Equal(OwnerType.Container, returnedPlayerStack.Ownership.OwnerType);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(
                MakeCommand(new FleeTheftEncounterAction(), Guid.NewGuid()),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterBelongsToAnotherWorld()
    {
        // Arrange
        var encounter = await SeedEncounter(
            sourceOwnerId: _owner.Id,
            sourceOwnerType: OwnerType.Creature,
            confrontingCreature: _confronter
        );
        var command = new ResolveTheftEncounterActionCommand
        {
            Action = new FleeTheftEncounterAction(),
            EncounterId = encounter.Id,
            PlayerId = _player.Id,
            SessionId = _session.Id,
            WorldId = Guid.NewGuid(),
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterBelongsToAnotherPlayer()
    {
        // Arrange
        var encounter = await SeedEncounter(
            sourceOwnerId: _owner.Id,
            sourceOwnerType: OwnerType.Creature,
            confrontingCreature: _confronter
        );
        var command = new ResolveTheftEncounterActionCommand
        {
            Action = new FleeTheftEncounterAction(),
            EncounterId = encounter.Id,
            PlayerId = Guid.NewGuid(),
            SessionId = _session.Id,
            WorldId = WorldId,
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ThrowsInvalidOperation_WhenTheEncounterIsAlreadyCompleted()
    {
        // Arrange
        var encounter = await SeedEncounter(
            sourceOwnerId: _owner.Id,
            sourceOwnerType: OwnerType.Creature,
            confrontingCreature: _confronter
        );
        await _handler.Handle(
            MakeCommand(new FleeTheftEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                MakeCommand(new FleeTheftEncounterAction(), encounter.Id),
                TestContext.Current.CancellationToken
            )
        );
    }

    private ResolveTheftEncounterActionCommand MakeCommand(
        TheftEncounterAction action,
        Guid encounterId
    ) =>
        new()
        {
            Action = action,
            EncounterId = encounterId,
            PlayerId = _player.Id,
            SessionId = _session.Id,
            WorldId = WorldId,
        };

    [Fact]
    public async Task Handle_Flee_ReportsTheItemsAsHeld_WhenTheyTransferredBeforeTheConfrontation()
    {
        // Arrange — taken from a container, so the player already had them when caught
        var item = Builders.MakeWeapon(WorldId);
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var encounter = await SeedEncounter(
            sourceOwnerId: Guid.NewGuid(),
            sourceOwnerType: OwnerType.Container,
            item: item,
            confrontingCreature: _confronter
        );

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new FleeTheftEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(fact.ItemsHeldByPlayer);
    }

    [Fact]
    public async Task Handle_Flee_ReportsTheItemsAsNotHeld_WhenTheAttemptWasCaughtBeforeTheyTransferred()
    {
        // Arrange — a caught pickpocket never receives the item, so fleeing leaves empty-handed
        var item = Builders.MakeWeapon(WorldId);
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var encounter = await SeedEncounter(
            sourceOwnerId: _owner.Id,
            sourceOwnerType: OwnerType.Creature,
            item: item,
            confrontingCreature: _confronter,
            itemsTransferred: false
        );

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new FleeTheftEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(fact.ItemsHeldByPlayer);
        Assert.Equal([item.Name], fact.ItemNames);
    }

    private async Task<TheftEncounter> SeedEncounter(
        Guid sourceOwnerId,
        OwnerType sourceOwnerType,
        Item? item = null,
        Creature? confrontingCreature = null,
        Guid? locationId = null,
        bool itemsTransferred = true
    )
    {
        var confrontingCreatureToUse = confrontingCreature ?? _owner;
        var crime = new TheftCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = _locationId,
            OwnerCreatureId = _owner.Id,
            OwnerName = _owner.Name,
            SourceOwnerId = sourceOwnerId,
            SourceOwnerType = sourceOwnerType,
            Items = item == null ? [] : [new TheftCrimeItem(item.Name, item.Quantity)],
        };
        var encounter = new TheftEncounter
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = locationId ?? _locationId,
            TheftCrimeId = crime.Id,
            ConfrontingCreatureId = confrontingCreatureToUse.Id,
            ConfrontingName = confrontingCreatureToUse.Name,
            SourceOwnerId = sourceOwnerId,
            SourceOwnerType = sourceOwnerType,
            ItemIds = item == null ? [] : [item.Id],
            ItemNames = item == null ? [] : [item.Name],
            ItemSelections =
                item == null || !itemsTransferred
                    ? []
                    : [new TheftEncounterItem(item.Id, item.Quantity)],
        };
        _context.Crimes.Add(crime);
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return encounter;
    }

    private sealed class AlwaysSuccessfulChanceRoller : IChanceRoller
    {
        public bool Roll(float chance) => true;
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
