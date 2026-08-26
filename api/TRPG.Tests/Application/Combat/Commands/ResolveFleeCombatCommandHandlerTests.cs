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

    private async Task<Creature> GetPlayer()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext.Creatures.SingleAsync(
            c => c.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
    }

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
    public async Task Handle_MovesPlayerToThePreviousLocation_WhenOneIsRecorded()
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
        Assert.Equal(previousLocation.Name, result!.DestinationLocationName);
        var movedPlayer = await GetPlayer();
        Assert.Equal(previousLocation.Id, movedPlayer.LocationId);
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
        Assert.Equal(outsideLocation.Name, result!.DestinationLocationName);
        var movedPlayer = await GetPlayer();
        Assert.Equal(outsideLocation.Id, movedPlayer.LocationId);
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
        _context.Locations.AddRange(lockedDestination, openDestination);
        _context.LocationConnectors.AddRange(lockedConnector, openConnector);
        _context.DoorConnectors.Add(lockedDoor);
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
        Assert.Equal(openDestination.Name, result!.DestinationLocationName);
        var movedPlayer = await GetPlayer();
        Assert.Equal(openDestination.Id, movedPlayer.LocationId);
    }

    [Fact]
    public async Task Handle_LeavesPlayerInPlace_WhenNoPreviousLocationAndNoUsableExits()
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
        Assert.Null(result!.DestinationLocationName);
        var movedPlayer = await GetPlayer();
        Assert.Equal(_currentLocation.Id, movedPlayer.LocationId);
    }
}
