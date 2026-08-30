using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Combat.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class ResolveFleeCombatCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid StateId = Guid.NewGuid();

    private readonly Location _currentLocation = Builders.MakeLocation(WorldId, StateId);
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _enemy = Builders.MakeCreature(
        WorldId,
        creatureType: CreatureType.Beast
    );

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private StartFightCommandHandler _startFight = null!;
    private ResolveFleeCombatCommandHandler _handler = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();

        _startFight = _serviceProvider.GetRequiredService<StartFightCommandHandler>();
        _handler = _serviceProvider.GetRequiredService<ResolveFleeCombatCommandHandler>();

        _player.LocationId = _currentLocation.Id;
        _enemy.LocationId = _currentLocation.Id;
        _session = Builders.MakeGameSession(WorldId, _player.Id);

        _context.Locations.Add(_currentLocation);
        _context.Creatures.AddRange(_player, _enemy);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task SeedFight() =>
        await _startFight.Handle(
            new StartFightCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
                EnemyCreatureIds = [_enemy.Id],
            },
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoFightIsActive()
    {
        // Act
        var result = await _handler.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ResolvesThePreviousLocation_WhenOneIsRecorded()
    {
        // Arrange
        var previousLocation = Builders.MakeLocation(WorldId, StateId);
        _context.Locations.Add(previousLocation);
        var trackedPlayer = await _context.Creatures.SingleAsync(
            c => c.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        trackedPlayer.PreviousLocationId = previousLocation.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedFight();

        // Act
        var result = await _handler.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(previousLocation.Id, result!.DestinationLocationId);
        Assert.Equal(previousLocation.Name, result.DestinationLocationName);
    }

    [Fact]
    public async Task Handle_PrefersTheOutsideExit_WhenNoPreviousLocationIsRecorded()
    {
        // Arrange
        var outsideLocation = Builders.MakeLocation(WorldId, StateId);
        var otherLocation = Builders.MakeLocation(WorldId, StateId);
        var outsideConnector = Builders.MakeLocationConnector(
            _currentLocation.Id,
            destinationLocationId: outsideLocation.Id,
            destinationLabel: "Outside"
        );
        var otherConnector = Builders.MakeLocationConnector(
            _currentLocation.Id,
            destinationLocationId: otherLocation.Id,
            destinationLabel: "North Hall"
        );
        _context.Locations.AddRange(outsideLocation, otherLocation);
        _context.LocationConnectors.AddRange(outsideConnector, otherConnector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedFight();

        // Act
        var result = await _handler.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(outsideLocation.Id, result!.DestinationLocationId);
        Assert.Equal(outsideLocation.Name, result.DestinationLocationName);
    }

    [Fact]
    public async Task Handle_SkipsLockedExits_WhenPickingAFallbackDestination()
    {
        // Arrange
        var lockedDestination = Builders.MakeLocation(WorldId, StateId);
        var openDestination = Builders.MakeLocation(WorldId, StateId);
        var lockedConnector = Builders.MakeLocationConnector(
            _currentLocation.Id,
            destinationLocationId: lockedDestination.Id,
            destinationLabel: "North Hall"
        );
        var openConnector = Builders.MakeLocationConnector(
            _currentLocation.Id,
            destinationLocationId: openDestination.Id,
            destinationLabel: "South Hall"
        );
        var lockedDoor = Builders.MakeDoorConnector(lockedConnector.Id, isLocked: true);
        var keyItem = Builders.MakeKey();
        _context.Locations.AddRange(lockedDestination, openDestination);
        _context.LocationConnectors.AddRange(lockedConnector, openConnector);
        _context.DoorConnectors.Add(lockedDoor);
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = lockedDoor.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedFight();

        // Act
        var result = await _handler.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(openDestination.Id, result!.DestinationLocationId);
        Assert.Equal(openDestination.Name, result.DestinationLocationName);
    }

    [Fact]
    public async Task Handle_ResolvesTheLockedExit_WhenThePlayerHasTheKey()
    {
        // Arrange
        var lockedDestination = Builders.MakeLocation(WorldId, StateId);
        var lockedConnector = Builders.MakeLocationConnector(
            _currentLocation.Id,
            destinationLocationId: lockedDestination.Id,
            destinationLabel: "North Hall"
        );
        var lockedDoor = Builders.MakeDoorConnector(lockedConnector.Id, isLocked: true);
        var keyItem = Builders.MakeKey();
        keyItem.Quantity = 1;
        keyItem.Ownership.OwnerId = _player.Id;
        keyItem.Ownership.OwnerType = OwnerType.Creature;
        _context.Locations.Add(lockedDestination);
        _context.LocationConnectors.Add(lockedConnector);
        _context.DoorConnectors.Add(lockedDoor);
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = lockedDoor.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedFight();

        // Act
        var result = await _handler.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(lockedDestination.Id, result!.DestinationLocationId);
        Assert.Equal(lockedDestination.Name, result.DestinationLocationName);
    }

    [Fact]
    public async Task Handle_ReturnsNoDestination_WhenNoPreviousLocationAndNoUsableExits()
    {
        // Arrange
        await SeedFight();

        // Act
        var result = await _handler.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = _session.Id,
                WorldId = WorldId,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result!.DestinationLocationId);
        Assert.Null(result.DestinationLocationName);
    }
}
