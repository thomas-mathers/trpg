using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Creatures.Mappers;
using TRPG.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Creatures.Queries;

[Collection("Database")]
public sealed class LocalMapMarkersTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private readonly Building _building = Builders.MakeBuilding(worldId: WorldId);
    private readonly Guid _currentLocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private GetLocalMapQueryHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<GetLocalMapQueryHandler>();
        _player = Builders.MakeCreature(WorldId, locationId: _currentLocationId);
        var room = Builders.MakeRoom(
            _building.Id,
            worldId: WorldId,
            locationId: _currentLocationId
        );
        _context.Buildings.Add(_building);
        _context.Rooms.Add(room);
        _context.Locations.Add(
            Builders.MakeLocation(WorldId, roomId: room.Id, id: room.LocationId)
        );
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Theory]
    [InlineData(false, 0, LocalMapMarkerState.Empty, LocalMapMarkerState.Unactivated)]
    [InlineData(true, 5, LocalMapMarkerState.ContainsItems, LocalMapMarkerState.Activated)]
    public async Task Handle_ReturnsCurrentPropStates_InExploredRooms(
        bool pulled,
        int gold,
        LocalMapMarkerState chestState,
        LocalMapMarkerState leverState
    )
    {
        // Arrange
        var chest = Builders.MakeContainer(WorldId, _currentLocationId);
        var lever = Builders.MakeLever(WorldId, _currentLocationId, isPulled: pulled);
        _context.Props.AddRange(chest, lever);
        _context.Items.Add(
            Builders.MakeGold(
                WorldId,
                quantity: gold,
                ownerId: chest.Id,
                ownerType: OwnerType.Container
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLocalMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var markers = Assert.Single(result.Rooms).Markers;
        Assert.Equal(chestState, Assert.Single(markers, marker => marker.Id == chest.Id).State);
        Assert.Equal(leverState, Assert.Single(markers, marker => marker.Id == lever.Id).State);
        Assert.Null(Assert.Single(markers, marker => marker.Id == chest.Id).ItemCount);
    }

    [Fact]
    public async Task Handle_HidesProps_InFrontierRooms()
    {
        // Arrange
        var frontier = Builders.MakeRoom(_building.Id, worldId: WorldId);
        _context.Rooms.Add(frontier);
        _context.LocationConnectors.Add(
            Builders.MakeLocationConnector(_currentLocationId, frontier.LocationId, WorldId)
        );
        _context.Props.AddRange(
            Builders.MakeContainer(WorldId, frontier.LocationId),
            Builders.MakeLever(WorldId, frontier.LocationId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLocalMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var room = Assert.Single(result.Rooms, room => room.Id == frontier.Id);
        Assert.True(room.IsFrontier);
        Assert.Empty(room.Markers);
    }

    [Theory]
    [InlineData(true, 2, 1)]
    [InlineData(true, 0, 0)]
    [InlineData(false, 2, 0)]
    public async Task Handle_ShowsOnlyOwnRecoverableCorpse_OnAnotherFloor(
        bool isOwner,
        int quantity,
        int expected
    )
    {
        // Arrange
        var lower = Builders.MakeRoom(_building.Id, worldId: WorldId, floorNumber: -1);
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: lower.LocationId,
            state: CreatureState.Dead,
            playerCorpseOwnerId: isOwner ? _player.Id : Guid.NewGuid()
        );
        _context.Rooms.Add(lower);
        _context.Creatures.Add(corpse);
        _context.Items.Add(Builders.MakeGold(WorldId, quantity: quantity, ownerId: corpse.Id));
        _context.Props.Add(Builders.MakeContainer(WorldId, lower.LocationId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLocalMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(expected, result.Rooms.SelectMany(room => room.Markers).Count());
        if (expected == 0)
            return;
        var room = Assert.Single(result.Rooms, room => room.FloorNumber == -1);
        var marker = Assert.Single(room.Markers);
        Assert.Equal(LocalMapMarkerKind.PlayerCorpse, marker.Kind);
        Assert.Equal(1, marker.ItemCount);
    }

    [Fact]
    public async Task Handle_RefreshesMarkers_AfterLootAndLeverChanges()
    {
        // Arrange
        var chest = Builders.MakeContainer(WorldId, _currentLocationId);
        var lever = Builders.MakeLever(WorldId, _currentLocationId);
        var gold = Builders.MakeGold(
            WorldId,
            quantity: 3,
            ownerId: chest.Id,
            ownerType: OwnerType.Container
        );
        _context.Props.AddRange(chest, lever);
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _handler.Handle(
            new GetLocalMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );
        await _context
            .Props.OfType<Lever>()
            .Where(prop => prop.Id == lever.Id)
            .ExecuteUpdateAsync(
                update => update.SetProperty(prop => prop.IsPulled, true),
                TestContext.Current.CancellationToken
            );
        await _context
            .Items.Where(item => item.Id == gold.Id)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLocalMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var markers = Assert.Single(result.Rooms).Markers;
        Assert.Equal(
            LocalMapMarkerState.Empty,
            Assert.Single(markers, marker => marker.Id == chest.Id).State
        );
        Assert.Equal(
            LocalMapMarkerState.Activated,
            Assert.Single(markers, marker => marker.Id == lever.Id).State
        );
    }

    [Theory]
    [InlineData(LocalMapLockKind.KeyLockedDoor)]
    [InlineData(LocalMapLockKind.Portcullis)]
    [InlineData(LocalMapLockKind.LockedDoor)]
    [InlineData(LocalMapLockKind.None)]
    public async Task Handle_ExposesLockKind_WithoutKeyOrLeverIdentities(LocalMapLockKind kind)
    {
        // Arrange
        var next = Builders.MakeRoom(_building.Id, worldId: WorldId);
        var connector = Builders.MakeLocationConnector(
            _currentLocationId,
            next.LocationId,
            WorldId
        );
        var door = Builders.MakeDoorConnector(
            connector.Id,
            isLocked: kind != LocalMapLockKind.None,
            worldId: WorldId
        );
        _context.Rooms.Add(next);
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        if (kind == LocalMapLockKind.KeyLockedDoor)
            _context.DoorConnectorKeys.Add(
                Builders.MakeDoorConnectorKey(Guid.NewGuid(), door.Id, WorldId)
            );
        if (kind == LocalMapLockKind.Portcullis)
            _context.DoorConnectorLevers.Add(
                Builders.MakeDoorConnectorLever(Guid.NewGuid(), door.Id, WorldId)
            );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLocalMapQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var passage = Assert.Single(result.Passages);
        Assert.Equal(kind, passage.LockKind);
        var response = passage.ToResponse();
        Assert.Equal(kind, response.LockKind);
        Assert.Equal(kind != LocalMapLockKind.None, response.IsLocked);
    }
}
