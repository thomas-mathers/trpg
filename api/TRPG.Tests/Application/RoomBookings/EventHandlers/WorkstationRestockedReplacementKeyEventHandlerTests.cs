using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.RoomBookings.EventHandlers;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.RoomBookings.EventHandlers;

public sealed class WorkstationRestockedReplacementKeyEventHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WorkstationRestockedReplacementKeyEventHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<WorkstationRestockedReplacementKeyEventHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_IssuesAKeyForTheDoor_WhenAnInnGuestRoomHasNoWorkstationOwnedKey()
    {
        // Arrange
        var inn = await SeedInnWithLockedGuestRoom();

        // Act
        await _handler.Handle(MakeEvent(inn), TestContext.Current.CancellationToken);

        // Assert
        var keys = await LoadWorkstationKeys(inn);
        var doorKey = Assert.Single(keys);
        Assert.Equal(inn.DoorId, doorKey.DoorConnectorId);
    }

    [Fact]
    public async Task Handle_IssuesNoSecondKey_WhenTheDoorAlreadyHasAWorkstationOwnedKey()
    {
        // Arrange
        var inn = await SeedInnWithLockedGuestRoom();
        var existingKey = Builders.MakeKey(
            worldId: WorldId,
            quantity: 1,
            ownerId: inn.WorkstationId,
            ownerType: OwnerType.Workstation
        );
        _context.Items.Add(existingKey);
        _context.DoorConnectorKeys.Add(
            Builders.MakeDoorConnectorKey(existingKey.Id, inn.DoorId, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeEvent(inn), TestContext.Current.CancellationToken);

        // Assert
        var keys = await LoadWorkstationKeys(inn);
        var doorKey = Assert.Single(keys);
        Assert.Equal(existingKey.Id, doorKey.ItemId);
    }

    [Fact]
    public async Task Handle_IssuesOneKeyOnly_WhenTheSameEventIsDeliveredTwice()
    {
        // Arrange
        var inn = await SeedInnWithLockedGuestRoom();
        var restocked = MakeEvent(inn);
        await _handler.Handle(restocked, TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(restocked, TestContext.Current.CancellationToken);

        // Assert
        var keys = await LoadWorkstationKeys(inn);
        Assert.Single(keys);
    }

    [Fact]
    public async Task Handle_IssuesNothing_WhenTheBuildingIsNotAnInn()
    {
        // Arrange
        var inn = await SeedInnWithLockedGuestRoom(BuildingType.Apothecary);

        // Act
        await _handler.Handle(MakeEvent(inn), TestContext.Current.CancellationToken);

        // Assert
        var keys = await LoadWorkstationKeys(inn);
        Assert.Empty(keys);
    }

    private static WorkstationRestockedEvent MakeEvent(SeededInn inn) =>
        new(WorldId, inn.BuildingId, inn.WorkstationId, GameClock.Epoch);

    private async Task<DoorConnectorKey[]> LoadWorkstationKeys(SeededInn inn)
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .DoorConnectorKeys.Where(k => k.DoorConnectorId == inn.DoorId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private async Task<SeededInn> SeedInnWithLockedGuestRoom(
        BuildingType buildingType = BuildingType.Inn
    )
    {
        var lobbyLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: buildingType);
        var lobby = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: lobbyLocationId,
            name: "Lobby"
        );
        var lobbyLocation = Builders.MakeLocation(WorldId, roomId: lobby.Id, id: lobbyLocationId);
        var counter = Builders.MakeWorkstation(worldId: WorldId, locationId: lobbyLocationId);
        var guestRoom = Builders.MakeRoom(building.Id, worldId: WorldId, name: "North Guest Room");
        var guestRoomLocation = Builders.MakeLocation(
            WorldId,
            roomId: guestRoom.Id,
            id: guestRoom.LocationId
        );
        var entryConnector = Builders.MakeLocationConnector(
            lobbyLocationId,
            destinationLocationId: guestRoom.LocationId,
            worldId: WorldId
        );
        var door = Builders.MakeDoorConnector(entryConnector.Id, isLocked: true, worldId: WorldId);

        _context.Buildings.Add(building);
        _context.Rooms.AddRange(lobby, guestRoom);
        _context.Locations.AddRange(lobbyLocation, guestRoomLocation);
        _context.Props.Add(counter);
        _context.LocationConnectors.Add(entryConnector);
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededInn(building.Id, counter.Id, door.Id);
    }

    private sealed record SeededInn(Guid BuildingId, Guid WorkstationId, Guid DoorId);
}
