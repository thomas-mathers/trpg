using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Scenes;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

[Collection("Database")]
public sealed class MovePlayerCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private MovePlayerCommandHandler _handler = null!;
    private GameSession _session = null!;
    private Location _outdoorLocation = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<MovePlayerCommandHandler>();

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
    public async Task Handle_EntersTheBuilding_WhenOutdoorsAndDestinationIsABuilding()
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
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "The Rusty Anchor",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(entranceRoom.LocationId, result.Player.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsDestinationNotFound_WhenTheBuildingIsInAnotherDistrict()
    {
        // Arrange - the building exists in the state but in a district the player isn't standing in
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
            new MovePlayerCommand
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
    public async Task Handle_ReturnsDoorLocked_WhenTheEntranceDoorIsLockedAndPlayerHasNoKey()
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
        var keyItem = new Item { Name = "Vault Key", Description = "A test key." };
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
            new MovePlayerCommand
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
    public async Task Handle_EntersTheBuilding_WhenTheEntranceDoorIsLockedButPlayerHasTheKey()
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
        var keyItem = new Item { Name = "Vault Key", Description = "A test key." };
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
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "The Guarded Vault",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(entranceRoom.LocationId, result.Player.LocationId);
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
            new MovePlayerCommand
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
    public async Task Handle_MovesThroughTheExit_WhenIndoorsAndDestinationMatchesAnExit()
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
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(nextRoom.LocationId, result.Player.LocationId);
    }

    [Fact]
    public async Task Handle_ResolvesWitnessedTheft_WhenThePlayerLeavesTheCrimeScene()
    {
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
            name: "Hallway",
            description: "A hallway.",
            destinationLabel: nextRoom.Name
        );
        var player = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        var witness = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        var faction = Builders.MakeFaction(WorldId);
        var crime = new TheftCrime
        {
            WorldId = WorldId,
            PlayerId = player.Id,
            LocationId = currentRoom.LocationId,
            OwnerFactionId = faction.Id,
            OwnerCreatureId = witness.Id,
            OwnerName = witness.Name,
            Outcome = TheftCrimeOutcome.Taken,
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Container,
        };
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
        _context.Locations.AddRange(currentLocation, nextLocation);
        _context.LocationConnectors.Add(connector);
        _context.Creatures.AddRange(player, witness);
        _context.Factions.Add(faction);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(
            new CrimeWitness
            {
                WorldId = WorldId,
                CrimeId = crime.Id,
                CreatureId = witness.Id,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        var persistedWitness = await verifyContext.CrimeWitnesses.SingleAsync(
            candidate => candidate.CrimeId == crime.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(CrimeResolution.Reported, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Reported, persistedWitness.Resolution);
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
            new MovePlayerCommand
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
    public async Task Handle_ReturnsDoorLocked_WhenTheInteriorConnectorIsLockedAndPlayerHasNoKey()
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
        var keyItem = new Item { Name = "Cell Key", Description = "A test key." };
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
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Locked, result.Outcome);
        Assert.Equal(currentRoom.LocationId, result.Player.LocationId);
    }

    [Fact]
    public async Task Handle_MovesThroughTheExit_WhenTheInteriorConnectorIsLockedButPlayerHasTheKey()
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
        var keyItem = new Item { Name = "Cell Key", Description = "A test key." };
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
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(nextRoom.LocationId, result.Player.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsDoorLocked_WhenTheTimedUnlockHasNotElapsedYet()
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
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Locked, result.Outcome);
        Assert.Equal(currentRoom.LocationId, result.Player.LocationId);
    }

    [Fact]
    public async Task Handle_MovesThroughTheExit_AndPersistsTheUnlock_WhenTheTimedUnlockHasElapsed()
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
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = session.Id,
                DestinationName = nextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(nextRoom.LocationId, result.Player.LocationId);

        await using var verifyContext = db.CreateContext();
        var updatedDoor = await verifyContext.DoorConnectors.FindAsync(
            [door.Id],
            TestContext.Current.CancellationToken
        );
        Assert.False(updatedDoor!.IsLocked);
        Assert.Null(updatedDoor.UnlocksAtPlaytime);
    }

    [Fact]
    public async Task Handle_MovesThroughAHubConnector_WhenAlreadyPlacedInAConnectedDistrict()
    {
        // Arrange - a placed (non-unplaced) player travels via a real hub LocationConnector, not the
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
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "City Center",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(cityCenter.LocationId, result.Player.LocationId);

        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(cityCenter.LocationId, updated!.LocationId);
    }

    [Fact]
    public async Task Handle_DeletesDeadCreaturesLeftBehindInTheOldDistrict()
    {
        // Arrange - the player is already placed in a real district connected to the destination
        // by a hub connector, exercising the normal (non-bootstrap) district-to-district move
        var stateId = Guid.NewGuid();
        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: stateId);
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: WorldId);
        var oldDistrictId = Guid.NewGuid();
        var oldLocation = Builders.MakeLocation(
            WorldId,
            stateId,
            cityId: city.Id,
            districtId: oldDistrictId
        );
        var oldDistrict = Builders.MakeDistrict(
            city.Id,
            DistrictType.Residential,
            worldId: WorldId,
            name: "Docks",
            id: oldDistrictId,
            locationId: oldLocation.Id
        );
        var newDistrictId = Guid.NewGuid();
        var newLocation = Builders.MakeLocation(
            WorldId,
            stateId,
            cityId: city.Id,
            districtId: newDistrictId
        );
        var newDistrict = Builders.MakeDistrict(
            city.Id,
            worldId: WorldId,
            name: "Market Row",
            id: newDistrictId,
            locationId: newLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            oldDistrict.LocationId,
            destinationLocationId: newDistrict.LocationId,
            name: "Path",
            description: "A path leading to Market Row.",
            destinationLabel: newDistrict.Name
        );
        var player = Builders.MakeCreature(WorldId, locationId: oldDistrict.LocationId);
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: oldDistrict.LocationId,
            state: CreatureState.Dead
        );
        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.Districts.AddRange(oldDistrict, newDistrict);
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.LocationConnectors.Add(connector);
        _context.Creatures.AddRange(player, corpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Market Row",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var remainingCorpse = await verifyContext.Creatures.FindAsync(
            [corpse.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Null(remainingCorpse);
    }

    [Fact]
    public async Task Handle_KeepsCorpse_WhenItHoldsAnActiveQuestItem()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: oldLocation.Id,
            state: CreatureState.Dead
        );
        var quest = Builders.MakeQuest(corpse.Id, WorldId);
        var item = Builders.MakeWeapon(WorldId);
        item.Ownership.OwnerId = corpse.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        var objective = new CollectItemObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            ItemId = item.Id,
        };
        var connector = Builders.MakeLocationConnector(
            oldLocation.Id,
            destinationLocationId: newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, corpse);
        _context.Quests.Add(quest);
        _context.Items.Add(item);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = player.Id,
                ObjectiveId = objective.Id,
                WorldId = WorldId,
            }
        );
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Elsewhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.NotNull(
            await verifyContext.Creatures.FindAsync(
                [corpse.Id],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_KeepsCorpse_WhenItIsAPlayerCorpse()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: oldLocation.Id,
            state: CreatureState.Dead,
            playerCorpseOwnerId: player.Id
        );
        var connector = Builders.MakeLocationConnector(
            oldLocation.Id,
            destinationLocationId: newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, corpse);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Elsewhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.NotNull(
            await verifyContext.Creatures.FindAsync(
                [corpse.Id],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_CreatesAnActiveEncounter_WhenMovingIntoALocationWithAnEngagingGroup()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id, level: 1);
        var faction = Builders.MakeFaction(WorldId, aggression: 150);
        var monster = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: newLocation.Id,
            level: 1
        );
        var group = Builders.MakeEncounterGroup(WorldId, newLocation.Id, faction.Id);
        var member = Builders.MakeEncounterGroupMember(WorldId, group.Id, monster.Id);
        var connector = Builders.MakeLocationConnector(
            oldLocation.Id,
            destinationLocationId: newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, monster);
        _context.Factions.Add(faction);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(member);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Elsewhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result.Encounter);
        Assert.Equal(faction.Name, result.Encounter.FactionName);

        await using var verifyContext = db.CreateContext();
        var encounter = await verifyContext
            .Encounters.OfType<HostileEncounter>()
            .SingleAsync(e => e.PlayerId == player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EncounterState.Active, encounter.State);
        Assert.Equal(oldLocation.Id, encounter.ArrivalOriginLocationId);
    }

    [Fact]
    public async Task Handle_CreatesAnActiveGuardEncounter_WhenMovingToALowRepGuardsLocation()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);
        var guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: newLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            oldLocation.Id,
            destinationLocationId: newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, guard);
        _context.Factions.Add(cityFaction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, cityFaction.Id, guard.Id));
        _context.Reputations.Add(
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = player.Id,
                TargetId = cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                Score = -50,
            }
        );
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["EncounterChance"] = 1f.ToString(CultureInfo.InvariantCulture),
                }
            )
            .Build();
        await using var guardServiceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<GuardEncounterOptions>(configuration)
            .BuildServiceProvider();
        var guardHandler = guardServiceProvider.GetRequiredService<MovePlayerCommandHandler>();

        // Act
        var result = await guardHandler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Elsewhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result.GuardEncounter);
        Assert.Equal(guard.Id, result.GuardEncounter.GuardCreatureId);

        await using var verifyContext = db.CreateContext();
        var encounter = await verifyContext
            .Encounters.OfType<GuardEncounter>()
            .SingleAsync(e => e.PlayerId == player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EncounterState.Active, encounter.State);
    }

    [Fact]
    public async Task Handle_EvictsCatchUpCacheAndClearsAlert_WhenLeavingALocationWithAnAlertedCreature()
    {
        // Arrange — the session's fresh Playtime maps to in-game hour 8
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var alertedMonster = Builders.MakeCreature(
            WorldId,
            locationId: oldLocation.Id,
            state: CreatureState.Alerted
        );
        var connector = Builders.MakeLocationConnector(
            oldLocation.Id,
            destinationLocationId: newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, alertedMonster);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var catchUpCache = _serviceProvider.GetRequiredService<SceneCatchUpCache>();
        catchUpCache.MarkCaughtUp(WorldId, oldLocation.Id, hour: 8);

        // Act
        await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Elsewhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(catchUpCache.HasCaughtUp(WorldId, oldLocation.Id, hour: 8));

        await using var verifyContext = db.CreateContext();
        var updatedMonster = await verifyContext.Creatures.FindAsync(
            [alertedMonster.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Idle, updatedMonster!.State);
    }

    [Fact]
    public async Task Handle_ReturnsAlertedCreatureToItsSchedule_WhenPlayerLeavesAndComesBack()
    {
        // Arrange — the session's fresh Playtime maps to in-game hour 8
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var alertedMonster = Builders.MakeCreature(
            WorldId,
            locationId: oldLocation.Id,
            state: CreatureState.Alerted
        );
        var sleepJob = Builders.MakeCreatureJob(
            alertedMonster.Id,
            action: CreatureJobAction.Sleep,
            startHour: 6,
            endHour: 10,
            locationId: oldLocation.Id,
            worldId: WorldId
        );
        var outboundConnector = Builders.MakeLocationConnector(
            oldLocation.Id,
            destinationLocationId: newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        var returnConnector = Builders.MakeLocationConnector(
            newLocation.Id,
            destinationLocationId: oldLocation.Id,
            destinationLabel: "Back"
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, alertedMonster);
        _context.CreatureJobs.Add(sleepJob);
        _context.LocationConnectors.AddRange(outboundConnector, returnConnector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var catchUpCache = _serviceProvider.GetRequiredService<SceneCatchUpCache>();
        catchUpCache.MarkCaughtUp(WorldId, oldLocation.Id, hour: 8);

        // Act
        await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Elsewhere",
            },
            TestContext.Current.CancellationToken
        );
        await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Back",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedMonster = await verifyContext.Creatures.FindAsync(
            [alertedMonster.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Sleeping, updatedMonster!.State);
    }

    [Fact]
    public async Task Handle_ReturnsEncounterActive_WithoutMoving_WhenPlayerHasAnActiveEncounter()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var faction = Builders.MakeFaction(WorldId);
        var group = Builders.MakeEncounterGroup(WorldId, oldLocation.Id, faction.Id);
        var activeEncounter = Builders.MakeHostileEncounter(WorldId, player.Id, oldLocation.Id);
        var connector = Builders.MakeLocationConnector(
            oldLocation.Id,
            destinationLocationId: newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.Add(player);
        _context.Factions.Add(faction);
        _context.EncounterGroups.Add(group);
        _context.Encounters.Add(activeEncounter);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Elsewhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.EncounterActive, result.Outcome);
        Assert.Equal(oldLocation.Id, result.Player.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsEncounterActive_WithoutMoving_WhenPlayerHasAnActiveFight()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var activeFight = Builders.MakeFight(WorldId, player.Id, [player.Id]);
        var connector = Builders.MakeLocationConnector(
            oldLocation.Id,
            destinationLocationId: newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.Add(player);
        _context.Encounters.Add(activeFight);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Elsewhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.EncounterActive, result.Outcome);
        Assert.Equal(oldLocation.Id, result.Player.LocationId);
    }
}
