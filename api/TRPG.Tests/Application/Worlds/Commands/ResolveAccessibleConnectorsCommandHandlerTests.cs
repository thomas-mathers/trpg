using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class ResolveAccessibleConnectorsCommandHandlerTests
{
    private readonly FakeGetDoorConnectorsByConnectorIdsQueryHandler _getDoorConnectors = new();
    private readonly FakeGetKeyItemIdsByDoorConnectorIdsQueryHandler _getKeyItemIds = new();
    private readonly FakeGetLeverIdsByDoorConnectorIdsQueryHandler _getLeverIds = new();
    private readonly FakeSetDoorTimedLockCommandHandler _setDoorTimedLock = new();
    private readonly ResolveAccessibleConnectorsCommandHandler _handler;

    public ResolveAccessibleConnectorsCommandHandlerTests() =>
        _handler = new ResolveAccessibleConnectorsCommandHandler(
            _getDoorConnectors,
            _setDoorTimedLock,
            _getKeyItemIds,
            _getLeverIds
        );

    private static ResolveAccessibleConnectorsCommand MakeCommand(
        Guid connectorId,
        IReadOnlySet<Guid>? playerKeyItemIds = null,
        IReadOnlySet<Guid>? pulledLeverIds = null,
        TimeSpan? playtime = null
    ) =>
        new()
        {
            PlayerKeyItemIds = playerKeyItemIds ?? new HashSet<Guid>(),
            PulledLeverIds = pulledLeverIds ?? new HashSet<Guid>(),
            Playtime = playtime ?? TimeSpan.Zero,
            ConnectorIds = [connectorId],
        };

    [Fact]
    public async Task Handle_ReturnsAllConnectors_WhenNoneAreLocked()
    {
        // Arrange
        var connectorId = Guid.NewGuid();

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(connectorId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connectorId], accessible);
    }

    [Fact]
    public async Task Handle_ReturnsConnector_WhenLockedButNoKeyWasEverConfigured()
    {
        // Arrange - a lock with no key configured would otherwise soft-lock the building forever
        var connectorId = Guid.NewGuid();
        var door = new DoorConnector { ConnectorId = connectorId, IsLocked = true };
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector> { [connectorId] = door };

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(connectorId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connectorId], accessible);
    }

    [Fact]
    public async Task Handle_ExcludesConnector_WhenLockedWithAKeyThePlayerDoesNotHold()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        var door = new DoorConnector { ConnectorId = connectorId, IsLocked = true };
        var keyItemId = Guid.NewGuid();
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector> { [connectorId] = door };
        _getKeyItemIds.KeyItemIdsByDoor = new Dictionary<Guid, IReadOnlyList<Guid>>
        {
            [door.Id] = [keyItemId],
        };

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(connectorId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(accessible);
    }

    [Fact]
    public async Task Handle_ReturnsConnector_WhenLockedAndPlayerHoldsTheConfiguredKey()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        var door = new DoorConnector { ConnectorId = connectorId, IsLocked = true };
        var keyItemId = Guid.NewGuid();
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector> { [connectorId] = door };
        _getKeyItemIds.KeyItemIdsByDoor = new Dictionary<Guid, IReadOnlyList<Guid>>
        {
            [door.Id] = [keyItemId],
        };

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(connectorId, playerKeyItemIds: new HashSet<Guid> { keyItemId }),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connectorId], accessible);
    }

    [Fact]
    public async Task Handle_ExcludesConnector_WhenTimedUnlockHasNotElapsedYet()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        var door = new DoorConnector
        {
            ConnectorId = connectorId,
            IsLocked = true,
            UnlocksAtPlaytime = TimeSpan.FromHours(10),
        };
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector> { [connectorId] = door };

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(connectorId, playtime: TimeSpan.FromHours(5)),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(accessible);
    }

    [Fact]
    public async Task Handle_ReturnsConnector_AndClearsTheTimedLock_WhenTimedUnlockHasElapsed()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        var door = new DoorConnector
        {
            ConnectorId = connectorId,
            IsLocked = true,
            UnlocksAtPlaytime = TimeSpan.FromHours(5),
        };
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector> { [connectorId] = door };

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(connectorId, playtime: TimeSpan.FromHours(10)),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connectorId], accessible);
        Assert.Equal([door.Id], _setDoorTimedLock.LastCommand?.DoorConnectorIds);
        Assert.Null(_setDoorTimedLock.LastCommand?.UnlocksAtPlaytime);
    }

    [Fact]
    public async Task Handle_ExcludesConnector_WhenGatedByALeverThatHasNotBeenPulled()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        var door = new DoorConnector { ConnectorId = connectorId, IsLocked = true };
        var leverId = Guid.NewGuid();
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector> { [connectorId] = door };
        _getLeverIds.LeverIdsByDoor = new Dictionary<Guid, IReadOnlyList<Guid>>
        {
            [door.Id] = [leverId],
        };

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(connectorId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(accessible);
    }

    [Fact]
    public async Task Handle_ExcludesConnector_WhenOnlySomeOfItsGatingLeversArePulled()
    {
        // Arrange - an AND-gate: every contributing lever must be pulled, not just one of them.
        var connectorId = Guid.NewGuid();
        var door = new DoorConnector { ConnectorId = connectorId, IsLocked = true };
        var pulledLeverId = Guid.NewGuid();
        var unpulledLeverId = Guid.NewGuid();
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector> { [connectorId] = door };
        _getLeverIds.LeverIdsByDoor = new Dictionary<Guid, IReadOnlyList<Guid>>
        {
            [door.Id] = [pulledLeverId, unpulledLeverId],
        };

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(connectorId, pulledLeverIds: new HashSet<Guid> { pulledLeverId }),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(accessible);
    }

    [Fact]
    public async Task Handle_ReturnsConnector_WhenEveryGatingLeverHasBeenPulled()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        var door = new DoorConnector { ConnectorId = connectorId, IsLocked = true };
        var firstLeverId = Guid.NewGuid();
        var secondLeverId = Guid.NewGuid();
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector> { [connectorId] = door };
        _getLeverIds.LeverIdsByDoor = new Dictionary<Guid, IReadOnlyList<Guid>>
        {
            [door.Id] = [firstLeverId, secondLeverId],
        };

        // Act
        var accessible = await _handler.Handle(
            MakeCommand(
                connectorId,
                pulledLeverIds: new HashSet<Guid> { firstLeverId, secondLeverId }
            ),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([connectorId], accessible);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheAccessibleConnectors_WhenSomeAreLockedAndSomeAreNot()
    {
        // Arrange
        var openConnectorId = Guid.NewGuid();
        var lockedConnectorId = Guid.NewGuid();
        var door = new DoorConnector { ConnectorId = lockedConnectorId, IsLocked = true };
        var keyItemId = Guid.NewGuid();
        _getDoorConnectors.Doors = new Dictionary<Guid, DoorConnector>
        {
            [lockedConnectorId] = door,
        };
        _getKeyItemIds.KeyItemIdsByDoor = new Dictionary<Guid, IReadOnlyList<Guid>>
        {
            [door.Id] = [keyItemId],
        };

        // Act
        var accessible = await _handler.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = new HashSet<Guid>(),
                PulledLeverIds = new HashSet<Guid>(),
                Playtime = TimeSpan.Zero,
                ConnectorIds = [openConnectorId, lockedConnectorId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([openConnectorId], accessible);
    }

    private sealed class FakeGetDoorConnectorsByConnectorIdsQueryHandler
        : IQueryHandler<
            GetDoorConnectorsByConnectorIdsQuery,
            IReadOnlyDictionary<Guid, DoorConnector>
        >
    {
        public IReadOnlyDictionary<Guid, DoorConnector> Doors { get; set; } =
            new Dictionary<Guid, DoorConnector>();

        public Task<IReadOnlyDictionary<Guid, DoorConnector>> Handle(
            GetDoorConnectorsByConnectorIdsQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Doors);
    }

    private sealed class FakeGetKeyItemIdsByDoorConnectorIdsQueryHandler
        : IQueryHandler<
            GetKeyItemIdsByDoorConnectorIdsQuery,
            IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
        >
    {
        public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> KeyItemIdsByDoor { get; set; } =
            new Dictionary<Guid, IReadOnlyList<Guid>>();

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> Handle(
            GetKeyItemIdsByDoorConnectorIdsQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(KeyItemIdsByDoor);
    }

    private sealed class FakeGetLeverIdsByDoorConnectorIdsQueryHandler
        : IQueryHandler<
            GetLeverIdsByDoorConnectorIdsQuery,
            IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
        >
    {
        public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> LeverIdsByDoor { get; set; } =
            new Dictionary<Guid, IReadOnlyList<Guid>>();

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> Handle(
            GetLeverIdsByDoorConnectorIdsQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(LeverIdsByDoor);
    }

    private sealed class FakeSetDoorTimedLockCommandHandler
        : ICommandHandler<SetDoorTimedLockCommand>
    {
        public SetDoorTimedLockCommand? LastCommand { get; private set; }

        public Task Handle(
            SetDoorTimedLockCommand command,
            CancellationToken cancellationToken = default
        )
        {
            LastCommand = command;
            return Task.CompletedTask;
        }
    }
}
