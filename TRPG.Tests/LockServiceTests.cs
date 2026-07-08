using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class LockServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private Building _building = null!;
    private BuildingService _buildingService = null!;
    private TrpgDbContext _context = null!;
    private Room _entranceRoom = null!;
    private RoomConnector _frontDoor = null!;
    private InventoryService _inventoryService = null!;
    private LockService _service = null!;
    private Guid _stateId;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _buildingService = new BuildingService(_context, new MemoryCache(new MemoryCacheOptions()));
        _inventoryService = new InventoryService(_context);
        _service = new LockService(_buildingService, _inventoryService);

        _stateId = Guid.NewGuid();
        _building = Builders.MakeBuilding(_stateId);
        _entranceRoom = Builders.MakeRoom(_building.Id);
        _frontDoor = new RoomConnector {
            RoomId = _entranceRoom.Id, Name = "Front Door", Description = "The door leading outside.",
            DestinationRoomId = null, IsLocked = false
        };

        _context.Buildings.Add(_building);
        _context.Rooms.Add(_entranceRoom);
        _context.Props.Add(_frontDoor);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CanEnter_ReturnsTrue_WhenNotLocked() {
        // Act
        var canEnter = await _service.CanEnter(_entranceRoom.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(canEnter);
    }

    [Fact]
    public async Task CanEnter_ReturnsFalse_WhenLockedAndNoKey() {
        // Arrange — a key exists for this door, just not in the entering creature's inventory
        await _buildingService.SetFrontDoorLocked(_building.Id, true, TestContext.Current.CancellationToken);
        var keyItem = new Item { Name = "Test Key", Description = "A test key." };
        _context.Items.Add(keyItem);
        _context.RoomConnectorKeys.Add(new RoomConnectorKey { ItemId = keyItem.Id, RoomConnectorId = _frontDoor.Id });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var canEnter = await _service.CanEnter(_entranceRoom.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canEnter);
    }

    [Fact]
    public async Task CanEnter_ReturnsTrue_WhenLockedAndCarryingKey() {
        // Arrange
        await _buildingService.SetFrontDoorLocked(_building.Id, true, TestContext.Current.CancellationToken);
        var player = Builders.MakeCreature(stateId: _stateId);
        var keyItem = new Item { Name = "Test Key", Description = "A test key." };
        _context.Creatures.Add(player);
        _context.Items.Add(keyItem);
        _context.RoomConnectorKeys.Add(new RoomConnectorKey { ItemId = keyItem.Id, RoomConnectorId = _frontDoor.Id });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _inventoryService.Add(player.Id, keyItem.Id, 1, TestContext.Current.CancellationToken);

        // Act
        var canEnter = await _service.CanEnter(_entranceRoom.Id, player.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(canEnter);
    }

    [Fact]
    public async Task CanEnter_ReturnsTrue_ForEachDistinctKeyToTheSameDoor() {
        // Arrange — two residents, each with their own distinct key item, both unlocking the same door
        await _buildingService.SetFrontDoorLocked(_building.Id, true, TestContext.Current.CancellationToken);
        var residentA = Builders.MakeCreature(stateId: _stateId);
        var residentB = Builders.MakeCreature(stateId: _stateId);
        var keyA = new Item { Name = "Key A", Description = "Resident A's key." };
        var keyB = new Item { Name = "Key B", Description = "Resident B's key." };
        _context.Creatures.AddRange(residentA, residentB);
        _context.Items.AddRange(keyA, keyB);
        _context.RoomConnectorKeys.AddRange(
            new RoomConnectorKey { ItemId = keyA.Id, RoomConnectorId = _frontDoor.Id },
            new RoomConnectorKey { ItemId = keyB.Id, RoomConnectorId = _frontDoor.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _inventoryService.Add(residentA.Id, keyA.Id, 1, TestContext.Current.CancellationToken);
        await _inventoryService.Add(residentB.Id, keyB.Id, 1, TestContext.Current.CancellationToken);

        // Act
        var canEnterA = await _service.CanEnter(_entranceRoom.Id, residentA.Id, TestContext.Current.CancellationToken);
        var canEnterB = await _service.CanEnter(_entranceRoom.Id, residentB.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(canEnterA);
        Assert.True(canEnterB);
    }

    [Fact]
    public async Task CanEnter_ReturnsTrue_WhenLockedButNoKeyConfigured() {
        // Arrange
        var keylessDoorRoom = Builders.MakeRoom(_building.Id);
        var keylessDoor = new RoomConnector {
            RoomId = keylessDoorRoom.Id, Name = "Front Door", Description = "The door leading outside.",
            DestinationRoomId = null, IsLocked = true
        };
        _context.Rooms.Add(keylessDoorRoom);
        _context.Props.Add(keylessDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var canEnter =
            await _service.CanEnter(keylessDoorRoom.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(canEnter);
    }
}
