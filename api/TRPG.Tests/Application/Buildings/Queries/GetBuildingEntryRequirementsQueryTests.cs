using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Buildings.Results;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Queries;

[Collection("Database")]
public sealed class GetBuildingEntryRequirementsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private Building _building = null!;
    private SetFrontDoorLockedCommandHandler _setFrontDoorLocked = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private Room _entranceRoom = null!;
    private DoorConnector _frontDoor = null!;
    private GetBuildingEntryRequirementsQueryHandler _handler = null!;
    private Guid _stateId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _setFrontDoorLocked =
            _serviceProvider.GetRequiredService<SetFrontDoorLockedCommandHandler>();
        _handler = _serviceProvider.GetRequiredService<GetBuildingEntryRequirementsQueryHandler>();

        _stateId = Guid.NewGuid();
        _building = Builders.MakeBuilding();
        _entranceRoom = Builders.MakeRoom(_building.Id);
        var outsideLocation = Builders.MakeLocation(stateId: _stateId);
        var frontDoorConnector = Builders.MakeLocationConnector(
            _entranceRoom.LocationId,
            destinationLocationId: outsideLocation.Id,
            name: "Front Door",
            description: "The door leading outside."
        );
        _frontDoor = Builders.MakeDoorConnector(frontDoorConnector.Id);

        _context.Buildings.Add(_building);
        _context.Rooms.Add(_entranceRoom);
        _context.Locations.Add(outsideLocation);
        _context.LocationConnectors.Add(frontDoorConnector);
        _context.DoorConnectors.Add(_frontDoor);
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

    private async Task<Item> SeedKey(Guid doorConnectorId, string name = "Test Key")
    {
        var keyItem = new Item { Name = name, Description = "A test key." };
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = doorConnectorId }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return keyItem;
    }

    [Fact]
    public async Task Handle_ReturnsEntered_WhenNotLocked()
    {
        // Act
        var result = await _handler.Handle(
            new GetBuildingEntryRequirementsQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(BuildingEntryResult.Entered, result.Outcome);
        Assert.Equal(_entranceRoom.LocationId, result.EntranceLocationId);
        Assert.Null(result.ValidKeyItemIds);
    }

    [Fact]
    public async Task Handle_ReturnsLockedWithValidKeyItemIds_WhenLocked()
    {
        // Arrange — two distinct key items are registered to the same door
        await LockFrontDoor();
        var keyA = await SeedKey(_frontDoor.Id, "Key A");
        var keyB = await SeedKey(_frontDoor.Id, "Key B");

        // Act
        var result = await _handler.Handle(
            new GetBuildingEntryRequirementsQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );

        // Assert — the entrance location comes along so the caller can finish the check itself
        Assert.Equal(BuildingEntryResult.Locked, result.Outcome);
        Assert.Equal(_entranceRoom.LocationId, result.EntranceLocationId);
        Assert.Equal(
            new[] { keyA.Id, keyB.Id }.OrderBy(id => id),
            result.ValidKeyItemIds!.OrderBy(id => id)
        );
    }

    [Fact]
    public async Task Handle_ReturnsEntered_WhenLockedButNoKeyConfigured()
    {
        // Arrange — a separate building, since _building's floor-0 room is already taken
        var keylessBuilding = Builders.MakeBuilding();
        var keylessDoorRoom = Builders.MakeRoom(keylessBuilding.Id);
        var keylessDoorOutsideLocation = Builders.MakeLocation(stateId: _stateId);
        var keylessDoorConnector = Builders.MakeLocationConnector(
            keylessDoorRoom.LocationId,
            destinationLocationId: keylessDoorOutsideLocation.Id,
            name: "Front Door",
            description: "The door leading outside."
        );
        var keylessDoor = Builders.MakeDoorConnector(keylessDoorConnector.Id, isLocked: true);
        _context.Buildings.Add(keylessBuilding);
        _context.Rooms.Add(keylessDoorRoom);
        _context.Locations.Add(keylessDoorOutsideLocation);
        _context.LocationConnectors.Add(keylessDoorConnector);
        _context.DoorConnectors.Add(keylessDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetBuildingEntryRequirementsQuery { BuildingId = keylessBuilding.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(BuildingEntryResult.Entered, result.Outcome);
        Assert.Equal(keylessDoorRoom.LocationId, result.EntranceLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsNoEntrance_WhenBuildingHasNoRoomAtFloorZero()
    {
        // Arrange
        var emptyBuilding = Builders.MakeBuilding();
        _context.Buildings.Add(emptyBuilding);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetBuildingEntryRequirementsQuery { BuildingId = emptyBuilding.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(BuildingEntryResult.NoEntrance, result.Outcome);
        Assert.Null(result.EntranceLocationId);
    }
}
