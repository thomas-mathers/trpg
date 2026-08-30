using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Commands;

[Collection("Database")]
public sealed class ResolveAccessibleConnectorsCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveAccessibleConnectorsCommandHandler _handler = null!;
    private readonly Location _origin = Builders.MakeLocation(WorldId, Guid.NewGuid());

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveAccessibleConnectorsCommandHandler>();

        _context.Locations.Add(_origin);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsAllConnectors_WhenNoneAreLocked()
    {
        // Arrange
        var connector = Builders.MakeLocationConnector(_origin.Id, worldId: WorldId);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var accessible = await _handler.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = new HashSet<Guid>(),
                Playtime = TimeSpan.Zero,
                ConnectorIds = [connector.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connector.Id], accessible);
    }

    [Fact]
    public async Task Handle_ReturnsConnector_WhenLockedButNoKeyWasEverConfigured()
    {
        // Arrange - a lock with no key configured would otherwise soft-lock the building forever
        var connector = Builders.MakeLocationConnector(_origin.Id, worldId: WorldId);
        var door = Builders.MakeDoorConnector(connector.Id, isLocked: true, worldId: WorldId);
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var accessible = await _handler.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = new HashSet<Guid>(),
                Playtime = TimeSpan.Zero,
                ConnectorIds = [connector.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connector.Id], accessible);
    }

    [Fact]
    public async Task Handle_ExcludesConnector_WhenLockedWithAKeyThePlayerDoesNotHold()
    {
        // Arrange
        var connector = Builders.MakeLocationConnector(_origin.Id, worldId: WorldId);
        var door = Builders.MakeDoorConnector(connector.Id, isLocked: true, worldId: WorldId);
        var keyItemId = Guid.NewGuid();
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItemId, DoorConnectorId = door.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var accessible = await _handler.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = new HashSet<Guid>(),
                Playtime = TimeSpan.Zero,
                ConnectorIds = [connector.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(accessible);
    }

    [Fact]
    public async Task Handle_ReturnsConnector_WhenLockedAndPlayerHoldsTheConfiguredKey()
    {
        // Arrange
        var connector = Builders.MakeLocationConnector(_origin.Id, worldId: WorldId);
        var door = Builders.MakeDoorConnector(connector.Id, isLocked: true, worldId: WorldId);
        var keyItemId = Guid.NewGuid();
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItemId, DoorConnectorId = door.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var accessible = await _handler.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = new HashSet<Guid> { keyItemId },
                Playtime = TimeSpan.Zero,
                ConnectorIds = [connector.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connector.Id], accessible);
    }

    [Fact]
    public async Task Handle_ExcludesConnector_WhenTimedUnlockHasNotElapsedYet()
    {
        // Arrange
        var connector = Builders.MakeLocationConnector(_origin.Id, worldId: WorldId);
        var door = Builders.MakeDoorConnector(
            connector.Id,
            isLocked: true,
            worldId: WorldId,
            unlocksAtPlaytime: TimeSpan.FromHours(10)
        );
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var accessible = await _handler.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = new HashSet<Guid>(),
                Playtime = TimeSpan.FromHours(5),
                ConnectorIds = [connector.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(accessible);
    }

    [Fact]
    public async Task Handle_ReturnsConnector_AndPersistsTheUnlock_WhenTimedUnlockHasElapsed()
    {
        // Arrange
        var connector = Builders.MakeLocationConnector(_origin.Id, worldId: WorldId);
        var door = Builders.MakeDoorConnector(
            connector.Id,
            isLocked: true,
            worldId: WorldId,
            unlocksAtPlaytime: TimeSpan.FromHours(5)
        );
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var accessible = await _handler.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = new HashSet<Guid>(),
                Playtime = TimeSpan.FromHours(10),
                ConnectorIds = [connector.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connector.Id], accessible);

        await using var verifyContext = db.CreateContext();
        var updatedDoor = await verifyContext.DoorConnectors.SingleAsync(
            candidate => candidate.Id == door.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(updatedDoor.IsLocked);
        Assert.Null(updatedDoor.UnlocksAtPlaytime);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheAccessibleConnectors_WhenSomeAreLockedAndSomeAreNot()
    {
        // Arrange
        var openConnector = Builders.MakeLocationConnector(_origin.Id, worldId: WorldId);
        var lockedConnector = Builders.MakeLocationConnector(_origin.Id, worldId: WorldId);
        var door = Builders.MakeDoorConnector(lockedConnector.Id, isLocked: true, worldId: WorldId);
        var keyItemId = Guid.NewGuid();
        _context.LocationConnectors.AddRange(openConnector, lockedConnector);
        _context.DoorConnectors.Add(door);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItemId, DoorConnectorId = door.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var accessible = await _handler.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = new HashSet<Guid>(),
                Playtime = TimeSpan.Zero,
                ConnectorIds = [openConnector.Id, lockedConnector.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([openConnector.Id], accessible);
    }
}
