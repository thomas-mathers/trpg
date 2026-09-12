using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

public sealed class ResolveMoveDestinationCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
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

    private async Task<IndoorRoute> SeedIndoorRoute(
        string connectorName = "Hallway",
        string connectorDescription = "A hallway.",
        int destinationCapacity = 4
    )
    {
        var building = Builders.MakeBuilding(WorldId);
        var currentLocationId = Guid.NewGuid();
        var currentRoom = Builders.MakeRoom(building.Id, locationId: currentLocationId);
        var currentLocation = Builders.MakeLocation(
            WorldId,
            _stateId,
            id: currentLocationId,
            roomId: currentRoom.Id
        );
        var destinationLocationId = Guid.NewGuid();
        var destinationRoom = Builders.MakeRoom(
            building.Id,
            capacity: destinationCapacity,
            locationId: destinationLocationId
        );
        var destinationLocation = Builders.MakeLocation(
            WorldId,
            _stateId,
            id: destinationLocationId,
            roomId: destinationRoom.Id
        );
        var connector = Builders.MakeLocationConnector(
            currentLocation.Id,
            destinationLocationId: destinationLocation.Id,
            name: connectorName,
            description: connectorDescription,
            destinationLabel: destinationRoom.Name
        );
        var player = Builders.MakeCreature(WorldId, locationId: currentLocation.Id);
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, destinationRoom);
        _context.Locations.AddRange(currentLocation, destinationLocation);
        _context.LocationConnectors.Add(connector);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new IndoorRoute(destinationRoom, player);
    }

    private sealed record IndoorRoute(Room DestinationRoom, Creature Player);

    private async Task<EntranceRoute> SeedOutdoorBuildingEntrance(
        string buildingName,
        bool isLocked = false,
        bool playerHasKey = false
    )
    {
        var player = Builders.MakeCreature(WorldId, locationId: _outdoorLocation.Id);
        var building = Builders.MakeBuilding(
            exteriorLocationId: _outdoorLocation.Id,
            name: buildingName
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
            destinationLabel: buildingName
        );
        _context.Creatures.Add(player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(entranceRoom);
        _context.Locations.Add(entranceLocation);
        _context.LocationConnectors.Add(entryConnector);

        if (isLocked)
        {
            var door = Builders.MakeDoorConnector(entryConnector.Id, isLocked: true);
            var keyItem = Builders.MakeKey();
            if (playerHasKey)
            {
                keyItem.Quantity = 1;
                keyItem.Ownership.OwnerId = player.Id;
                keyItem.Ownership.OwnerType = OwnerType.Creature;
            }
            _context.DoorConnectors.Add(door);
            _context.Items.Add(keyItem);
            _context.DoorConnectorKeys.Add(
                new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = door.Id }
            );
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new EntranceRoute(player, entranceRoom);
    }

    private sealed record EntranceRoute(Creature Player, Room EntranceRoom);

    [Fact]
    public async Task Handle_ResolvesTheBuilding_WhenOutdoorsAndDestinationIsABuilding()
    {
        // Arrange
        var route = await SeedOutdoorBuildingEntrance("The Rusty Anchor");

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
                SessionId = _session.Id,
                DestinationName = "The Rusty Anchor",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(route.EntranceRoom.LocationId, result.DestinationLocationId);
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
        var route = await SeedOutdoorBuildingEntrance("The Locked Vault", isLocked: true);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
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
        var route = await SeedOutdoorBuildingEntrance(
            "The Guarded Vault",
            isLocked: true,
            playerHasKey: true
        );

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
                SessionId = _session.Id,
                DestinationName = "The Guarded Vault",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(route.EntranceRoom.LocationId, result.DestinationLocationId);
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
        var route = await SeedIndoorRoute();

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
                SessionId = _session.Id,
                DestinationName = route.DestinationRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(route.DestinationRoom.LocationId, result.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsExitNotFound_WhenIndoorsAndNoExitMatches()
    {
        // Arrange
        var route = await SeedIndoorRoute();

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
                SessionId = _session.Id,
                DestinationName = "Nowhere",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.ExitNotFound, result.Outcome);
    }

    private async Task<InteriorRoute> SeedInteriorLockedConnector(
        TimeSpan? unlocksAtPlaytime = null
    )
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
            name: "Cell Door",
            description: "A locked cell door.",
            destinationLabel: nextRoom.Name
        );
        var door = Builders.MakeDoorConnector(
            connector.Id,
            isLocked: true,
            unlocksAtPlaytime: unlocksAtPlaytime
        );
        var player = Builders.MakeCreature(WorldId, locationId: currentRoom.LocationId);
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
        _context.Locations.AddRange(currentLocation, nextLocation);
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(door);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new InteriorRoute(player, nextRoom, door);
    }

    private sealed record InteriorRoute(Creature Player, Room NextRoom, DoorConnector Door);

    [Fact]
    public async Task Handle_ReturnsLocked_WhenTheInteriorConnectorIsLockedAndPlayerHasNoKey()
    {
        // Arrange
        var route = await SeedInteriorLockedConnector();
        var guard = Builders.MakeCreature(WorldId, locationId: route.Player.LocationId);
        var keyItem = Builders.MakeKey();
        keyItem.Quantity = 1;
        keyItem.Ownership.OwnerId = guard.Id;
        keyItem.Ownership.OwnerType = OwnerType.Creature;
        _context.Creatures.Add(guard);
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = route.Door.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
                SessionId = _session.Id,
                DestinationName = route.NextRoom.Name,
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
        var route = await SeedInteriorLockedConnector();
        var keyItem = Builders.MakeKey();
        keyItem.Quantity = 1;
        keyItem.Ownership.OwnerId = route.Player.Id;
        keyItem.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(keyItem);
        _context.DoorConnectorKeys.Add(
            new DoorConnectorKey { ItemId = keyItem.Id, DoorConnectorId = route.Door.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
                SessionId = _session.Id,
                DestinationName = route.NextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(route.NextRoom.LocationId, result.DestinationLocationId);
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
        var route = await SeedInteriorLockedConnector(unlocksAtPlaytime: TimeSpan.FromHours(10));
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
                SessionId = session.Id,
                DestinationName = route.NextRoom.Name,
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
        var route = await SeedInteriorLockedConnector(unlocksAtPlaytime: TimeSpan.FromHours(5));
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = route.Player.Id,
                SessionId = session.Id,
                DestinationName = route.NextRoom.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.Entered, result.Outcome);
        Assert.Equal(route.NextRoom.LocationId, result.DestinationLocationId);

        await using var verifyContext = db.CreateContext();
        var updatedDoor = await verifyContext.DoorConnectors.FindAsync(
            [route.Door.Id],
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
