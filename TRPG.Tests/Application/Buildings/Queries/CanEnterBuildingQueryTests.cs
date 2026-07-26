using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Queries;

[Collection("Database")]
public sealed class CanEnterBuildingQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private Building _building = null!;
    private SetFrontDoorLockedCommandHandler _setFrontDoorLocked = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private Room _entranceRoom = null!;
    private RoomConnector _frontDoor = null!;
    private CanEnterBuildingQueryHandler _handler = null!;
    private Guid _stateId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _setFrontDoorLocked =
            _serviceProvider.GetRequiredService<SetFrontDoorLockedCommandHandler>();
        _handler = _serviceProvider.GetRequiredService<CanEnterBuildingQueryHandler>();

        _stateId = Guid.NewGuid();
        _building = Builders.MakeBuilding(_stateId);
        _entranceRoom = Builders.MakeRoom(_building.Id);
        _frontDoor = Builders.MakeRoomConnector(
            _entranceRoom.Id,
            name: "Front Door",
            description: "The door leading outside."
        );

        _context.Buildings.Add(_building);
        _context.Rooms.Add(_entranceRoom);
        _context.Props.Add(_frontDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private Task LockFrontDoor() =>
        _setFrontDoorLocked.Handle(
            new SetFrontDoorLockedCommand { BuildingId = _building.Id, IsLocked = true },
            TestContext.Current.CancellationToken
        );

    private async Task<Creature> SeedCreature()
    {
        var creature = Builders.MakeCreature(stateId: _stateId);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    private async Task<Item> SeedKey(Guid roomConnectorId, string name = "Test Key")
    {
        var keyItem = new Item { Name = name, Description = "A test key." };
        _context.Items.Add(keyItem);
        _context.RoomConnectorKeys.Add(
            new RoomConnectorKey { ItemId = keyItem.Id, RoomConnectorId = roomConnectorId }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return keyItem;
    }

    private async Task GiveKey(Guid creatureId, Guid itemId)
    {
        var item = await _context.Items.FirstAsync(
            i => i.Id == itemId,
            TestContext.Current.CancellationToken
        );
        item.Quantity = 1;
        item.Ownership.OwnerId = creatureId;
        item.Ownership.OwnerType = OwnerType.Creature;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_ReturnsTrue_WhenNotLocked()
    {
        // Act
        var canEnter = await _handler.Handle(
            new CanEnterBuildingQuery
            {
                EntranceRoomId = _entranceRoom.Id,
                EnteringCreatureId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(canEnter);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenLockedAndNoKey()
    {
        // Arrange — a key exists for this door, just not in the entering creature's inventory
        await LockFrontDoor();
        await SeedKey(_frontDoor.Id);

        // Act
        var canEnter = await _handler.Handle(
            new CanEnterBuildingQuery
            {
                EntranceRoomId = _entranceRoom.Id,
                EnteringCreatureId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(canEnter);
    }

    [Fact]
    public async Task Handle_ReturnsTrue_WhenLockedAndCarryingKey()
    {
        // Arrange
        await LockFrontDoor();
        var player = await SeedCreature();
        var keyItem = await SeedKey(_frontDoor.Id);
        await GiveKey(player.Id, keyItem.Id);

        // Act
        var canEnter = await _handler.Handle(
            new CanEnterBuildingQuery
            {
                EntranceRoomId = _entranceRoom.Id,
                EnteringCreatureId = player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(canEnter);
    }

    [Fact]
    public async Task Handle_ReturnsTrue_WhenCarryingTheFirstOfTwoKeysRegisteredToTheDoor()
    {
        // Arrange — two distinct key items are registered to the same door; the first must work too
        await LockFrontDoor();
        var resident = await SeedCreature();
        var keyA = await SeedKey(_frontDoor.Id, "Key A");
        await SeedKey(_frontDoor.Id, "Key B");
        await GiveKey(resident.Id, keyA.Id);

        // Act
        var canEnter = await _handler.Handle(
            new CanEnterBuildingQuery
            {
                EntranceRoomId = _entranceRoom.Id,
                EnteringCreatureId = resident.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(canEnter);
    }

    [Fact]
    public async Task Handle_ReturnsTrue_WhenCarryingTheSecondOfTwoKeysRegisteredToTheDoor()
    {
        // Arrange — two distinct key items are registered to the same door; the second must work too
        await LockFrontDoor();
        var resident = await SeedCreature();
        await SeedKey(_frontDoor.Id, "Key A");
        var keyB = await SeedKey(_frontDoor.Id, "Key B");
        await GiveKey(resident.Id, keyB.Id);

        // Act
        var canEnter = await _handler.Handle(
            new CanEnterBuildingQuery
            {
                EntranceRoomId = _entranceRoom.Id,
                EnteringCreatureId = resident.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(canEnter);
    }

    [Fact]
    public async Task Handle_ReturnsTrue_WhenLockedButNoKeyConfigured()
    {
        // Arrange
        var keylessDoorRoom = Builders.MakeRoom(_building.Id);
        var keylessDoor = Builders.MakeRoomConnector(
            keylessDoorRoom.Id,
            isLocked: true,
            name: "Front Door",
            description: "The door leading outside."
        );
        _context.Rooms.Add(keylessDoorRoom);
        _context.Props.Add(keylessDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var canEnter = await _handler.Handle(
            new CanEnterBuildingQuery
            {
                EntranceRoomId = keylessDoorRoom.Id,
                EnteringCreatureId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(canEnter);
    }
}
