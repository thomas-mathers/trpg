using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

[Collection("Database")]
public sealed class ResolveMoveDestinationCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveMoveDestinationCommandHandler _handler = null!;
    private GameSession _session = null!;
    private Location _outdoorLocation = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveMoveDestinationCommandHandler>();

        _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
        _outdoorLocation = Builders.MakeLocation(WorldId, _stateId);
        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);
        _context.GameSessions.Add(_session);
        _context.Locations.Add(_outdoorLocation);
        _context.States.Add(state);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ResolvesTheBuilding_WhenOutdoorsAndDestinationIsABuilding()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, locationId: _outdoorLocation.Id);
        var building = Builders.MakeBuilding(
            exteriorLocationId: _outdoorLocation.Id,
            name: "The Rusty Anchor"
        );
        var entranceRoomId = Guid.NewGuid();
        var entranceLocationId = Guid.NewGuid();
        var entranceRoom = Builders.MakeRoom(
            building.Id,
            id: entranceRoomId,
            locationId: entranceLocationId
        );
        var entranceLocation = Builders.MakeLocation(
            WorldId,
            _stateId,
            roomId: entranceRoomId,
            id: entranceLocationId
        );
        var entryConnector = Builders.MakeLocationConnector(
            _outdoorLocation.Id,
            destinationLocationId: entranceRoom.LocationId,
            name: "Front Door",
            description: "The door leading in.",
            destinationLabel: "The Rusty Anchor"
        );
        _context.Creatures.Add(player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(entranceRoom);
        _context.Locations.Add(entranceLocation);
        _context.LocationConnectors.Add(entryConnector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "The Rusty Anchor",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(entranceRoom.LocationId, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsDestinationNotFound_WhenTheBuildingIsInAnotherDistrict()
    {
        // Arrange - the building exists in the state but in a district the player isn't standing in
        var district = Builders.MakeLocation(WorldId, _stateId, districtId: Guid.NewGuid());
        var farLocation = Builders.MakeLocation(WorldId, _stateId, districtId: Guid.NewGuid());
        var player = Builders.MakeCreature(WorldId, locationId: district.Id);
        var farBuilding = Builders.MakeBuilding(
            exteriorLocationId: farLocation.Id,
            name: "The Distant Lighthouse"
        );
        _context.Locations.AddRange(district, farLocation);
        _context.Creatures.Add(player);
        _context.Buildings.Add(farBuilding);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "The Distant Lighthouse",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.DestinationNotFound, result.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsLocked_WhenTheEntranceDoorIsLockedAndPlayerHasNoKey()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, locationId: _outdoorLocation.Id);
        var building = Builders.MakeBuilding(
            exteriorLocationId: _outdoorLocation.Id,
            name: "The Locked Vault"
        );
        var entranceRoom = Builders.MakeRoom(building.Id);
        var entryConnector = Builders.MakeLocationConnector(
            _outdoorLocation.Id,
            destinationLocationId: entranceRoom.LocationId,
            name: "Front Door",
            description: "The door leading in.",
            destinationLabel: "The Locked Vault"
        );
        var door = Builders.MakeDoorConnector(entryConnector.Id, isLocked: true);
        var keyItem = Builders.MakeKey();
        _context.Creatures.Add(player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(entranceRoom);
        _context.LocationConnectors.Add(entryConnector);
        _context.DoorConnectors.Add(door);
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = door.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "The Locked Vault",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Locked, result.Outcome);
    }

    [Fact]
    public async Task Handle_ResolvesTheBuilding_WhenTheEntranceDoorIsLockedButPlayerHasTheKey()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, locationId: _outdoorLocation.Id);
        var building = Builders.MakeBuilding(
            exteriorLocationId: _outdoorLocation.Id,
            name: "The Guarded Vault"
        );
        var entranceRoomId = Guid.NewGuid();
        var entranceLocationId = Guid.NewGuid();
        var entranceRoom = Builders.MakeRoom(
            building.Id,
            id: entranceRoomId,
            locationId: entranceLocationId
        );
        var entranceLocation = Builders.MakeLocation(
            WorldId,
            _stateId,
            roomId: entranceRoomId,
            id: entranceLocationId
        );
        var entryConnector = Builders.MakeLocationConnector(
            _outdoorLocation.Id,
            destinationLocationId: entranceRoom.LocationId,
            name: "Front Door",
            description: "The door leading in.",
            destinationLabel: "The Guarded Vault"
        );
        var door = Builders.MakeDoorConnector(entryConnector.Id, isLocked: true);
        var keyItem = Builders.MakeKey();
        keyItem.Quantity = 1;
        keyItem.Ownership.OwnerId = player.Id;
        keyItem.Ownership.OwnerType = OwnerType.Creature;
        _context.Creatures.Add(player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(entranceRoom);
        _context.Locations.Add(entranceLocation);
        _context.LocationConnectors.Add(entryConnector);
        _context.DoorConnectors.Add(door);
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = door.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "The Guarded Vault",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(entranceRoom.LocationId, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsDestinationNotFound_WhenOutdoorsAndNothingMatches()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, locationId: _outdoorLocation.Id);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Nowhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.DestinationNotFound, result.Outcome);
    }

    [Fact]
    public async Task Handle_ResolvesTheExit_WhenIndoorsAndDestinationMatchesAnExit()
    {
        // Arrange
        var building = Builders.MakeBuilding();
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, _stateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var nextRoomId = Guid.NewGuid();
        var nextLocation = Builders.MakeLocation(WorldId, _stateId, roomId: nextRoomId);
        var nextRoom = Builders.MakeRoom(
            building.Id,
            capacity: 4,
            id: nextRoomId,
            locationId: nextLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            currentRoom.LocationId,
            destinationLocationId: nextRoom.LocationId,
            name: "Hallway",
            description: "A hallway.",
            destinationLabel: nextRoom.Name
        );
        var player = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
        _context.Locations.AddRange(currentLocation, nextLocation);
        _context.LocationConnectors.Add(connector);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(nextRoom.LocationId, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsExitNotFound_WhenIndoorsAndNoExitMatches()
    {
        // Arrange
        var building = Builders.MakeBuilding();
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, _stateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var player = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        _context.Buildings.Add(building);
        _context.Rooms.Add(currentRoom);
        _context.Locations.Add(currentLocation);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Nowhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.ExitNotFound, result.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsLocked_WhenTheInteriorConnectorIsLockedAndPlayerHasNoKey()
    {
        // Arrange
        var building = Builders.MakeBuilding();
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, _stateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var nextRoomId = Guid.NewGuid();
        var nextLocation = Builders.MakeLocation(WorldId, _stateId, roomId: nextRoomId);
        var nextRoom = Builders.MakeRoom(building.Id, id: nextRoomId, locationId: nextLocation.Id);
        var connector = Builders.MakeLocationConnector(
            currentRoom.LocationId,
            destinationLocationId: nextRoom.LocationId,
            name: "Cell Door",
            description: "A locked cell door.",
            destinationLabel: nextRoom.Name
        );
        var door = Builders.MakeDoorConnector(connector.Id, isLocked: true);
        var player = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        var guard = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        var keyItem = Builders.MakeKey();
        keyItem.Quantity = 1;
        keyItem.Ownership.OwnerId = guard.Id;
        keyItem.Ownership.OwnerType = OwnerType.Creature;
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
        _context.Locations.AddRange(currentLocation, nextLocation);
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        _context.Creatures.AddRange(player, guard);
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = door.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Locked, result.Outcome);
    }

    [Fact]
    public async Task Handle_ResolvesTheExit_WhenTheInteriorConnectorIsLockedButPlayerHasTheKey()
    {
        // Arrange
        var building = Builders.MakeBuilding();
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, _stateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var nextRoomId = Guid.NewGuid();
        var nextLocation = Builders.MakeLocation(WorldId, _stateId, roomId: nextRoomId);
        var nextRoom = Builders.MakeRoom(building.Id, id: nextRoomId, locationId: nextLocation.Id);
        var connector = Builders.MakeLocationConnector(
            currentRoom.LocationId,
            destinationLocationId: nextRoom.LocationId,
            name: "Cell Door",
            description: "A locked cell door.",
            destinationLabel: nextRoom.Name
        );
        var door = Builders.MakeDoorConnector(connector.Id, isLocked: true);
        var player = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        var keyItem = Builders.MakeKey();
        keyItem.Quantity = 1;
        keyItem.Ownership.OwnerId = player.Id;
        keyItem.Ownership.OwnerType = OwnerType.Creature;
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
        _context.Locations.AddRange(currentLocation, nextLocation);
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = door.Id }
        );
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(nextRoom.LocationId, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsLocked_WhenTheTimedUnlockHasNotElapsedYet()
    {
        // Arrange
        var session = Builders.MakeGameSession(
            WorldId,
            Guid.NewGuid(),
            playtime: TimeSpan.FromHours(5)
        );
        var building = Builders.MakeBuilding();
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, _stateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var nextRoomId = Guid.NewGuid();
        var nextLocation = Builders.MakeLocation(WorldId, _stateId, roomId: nextRoomId);
        var nextRoom = Builders.MakeRoom(building.Id, id: nextRoomId, locationId: nextLocation.Id);
        var connector = Builders.MakeLocationConnector(
            currentRoom.LocationId,
            destinationLocationId: nextRoom.LocationId,
            name: "Cell Door",
            description: "A locked cell door.",
            destinationLabel: nextRoom.Name
        );
        var door = Builders.MakeDoorConnector(
            connector.Id,
            isLocked: true,
            unlocksAtPlaytime: TimeSpan.FromHours(10)
        );
        var player = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        _context.GameSessions.Add(session);
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
        _context.Locations.AddRange(currentLocation, nextLocation);
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Locked, result.Outcome);
    }

    [Fact]
    public async Task Handle_ResolvesTheExit_AndPersistsTheUnlock_WhenTheTimedUnlockHasElapsed()
    {
        // Arrange
        var session = Builders.MakeGameSession(
            WorldId,
            Guid.NewGuid(),
            playtime: TimeSpan.FromHours(10)
        );
        var building = Builders.MakeBuilding();
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, _stateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var nextRoomId = Guid.NewGuid();
        var nextLocation = Builders.MakeLocation(WorldId, _stateId, roomId: nextRoomId);
        var nextRoom = Builders.MakeRoom(building.Id, id: nextRoomId, locationId: nextLocation.Id);
        var connector = Builders.MakeLocationConnector(
            currentRoom.LocationId,
            destinationLocationId: nextRoom.LocationId,
            name: "Cell Door",
            description: "A locked cell door.",
            destinationLabel: nextRoom.Name
        );
        var door = Builders.MakeDoorConnector(
            connector.Id,
            isLocked: true,
            unlocksAtPlaytime: TimeSpan.FromHours(5)
        );
        var player = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        _context.GameSessions.Add(session);
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
        _context.Locations.AddRange(currentLocation, nextLocation);
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(nextRoom.LocationId, result.DestinationLocationId);

        await using var verifyContext = db.CreateContext();
        var updatedDoor = await verifyContext.DoorConnectors.FindAsync(
            [door.Id],
            TestContext.Current.CancellationToken
        );
        Assert.False(updatedDoor!.IsLocked);
        Assert.Null(updatedDoor.UnlocksAtPlaytime);
    }

    [Fact]
    public async Task Handle_ResolvesAHubConnector_WhenAlreadyPlacedInAConnectedDistrict()
    {
        // Arrange - a placed (non-unplaced) player travels via a real hub LocationConnector, not the
        // unplaced-bootstrap GetDistrictByNameInCityQuery fallback the other district-move tests use
        var stateId = Guid.NewGuid();
        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: stateId);
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: WorldId);
        var cityCenterId = Guid.NewGuid();
        var cityCenterLocation = Builders.MakeLocation(
            WorldId,
            stateId,
            cityId: city.Id,
            districtId: cityCenterId
        );
        var cityCenter = Builders.MakeDistrict(
            city.Id,
            worldId: WorldId,
            name: "City Center",
            id: cityCenterId,
            locationId: cityCenterLocation.Id
        );
        var residentialId = Guid.NewGuid();
        var residentialLocation = Builders.MakeLocation(
            WorldId,
            stateId,
            cityId: city.Id,
            districtId: residentialId
        );
        var residential = Builders.MakeDistrict(
            city.Id,
            DistrictType.Residential,
            worldId: WorldId,
            name: "Docks",
            id: residentialId,
            locationId: residentialLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            residential.LocationId,
            destinationLocationId: cityCenter.LocationId,
            name: "Path",
            description: "A path leading to City Center.",
            destinationLabel: cityCenter.Name
        );
        var player = Builders.MakeCreature(WorldId, locationId: residential.LocationId);
        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.Districts.AddRange(cityCenter, residential);
        _context.Locations.AddRange(cityCenterLocation, residentialLocation);
        _context.LocationConnectors.Add(connector);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "City Center",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(cityCenter.LocationId, result.DestinationLocationId);
    }
}
