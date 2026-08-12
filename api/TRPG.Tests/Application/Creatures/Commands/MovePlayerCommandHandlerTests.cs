using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class MovePlayerCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid StateId = Guid.NewGuid();

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
        _outdoorLocation = Builders.MakeLocation(WorldId, StateId);
        _context.GameSessions.Add(_session);
        _context.Locations.Add(_outdoorLocation);
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
        var entranceRoom = Builders.MakeRoom(building.Id);
        _context.Creatures.Add(player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(entranceRoom);
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
        // Arrange Ã¢â‚¬â€ the building exists in the state but in a district the player isn't standing in
        var district = Builders.MakeLocation(WorldId, StateId, districtId: Guid.NewGuid());
        var farLocation = Builders.MakeLocation(WorldId, StateId, districtId: Guid.NewGuid());
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
    public async Task Handle_ReturnsBuildingHasNoEntrance_WhenTheBuildingHasNoRoomAtFloorZero()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, locationId: _outdoorLocation.Id);
        var building = Builders.MakeBuilding(
            exteriorLocationId: _outdoorLocation.Id,
            name: "The Empty Shell"
        );
        _context.Creatures.Add(player);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "The Empty Shell",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(EntryOutcome.NoEntrance, result.Outcome);
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
        var outsideLocation = Builders.MakeLocation(stateId: StateId);
        var frontDoor = Builders.MakeLocationConnector(
            entranceRoom.LocationId,
            destinationLocationId: outsideLocation.Id,
            name: "Front Door",
            description: "The door leading outside."
        );
        var door = Builders.MakeDoorConnector(frontDoor.Id, isLocked: true);
        var keyItem = new Item { Name = "Vault Key", Description = "A test key." };
        _context.Creatures.Add(player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(entranceRoom);
        _context.Locations.Add(outsideLocation);
        _context.LocationConnectors.Add(frontDoor);
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
        var currentLocation = Builders.MakeLocation(WorldId, StateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var nextRoomId = Guid.NewGuid();
        var nextLocation = Builders.MakeLocation(WorldId, StateId, roomId: nextRoomId);
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
    public async Task Handle_ReturnsExitNotFound_WhenIndoorsAndNoExitMatches()
    {
        // Arrange
        var building = Builders.MakeBuilding();
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, StateId, roomId: currentRoomId);
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
    public async Task Handle_MovesThroughAHubConnector_WhenAlreadyPlacedInAConnectedDistrict()
    {
        // Arrange Ã¢â‚¬â€ a placed (non-unplaced) player travels via a real hub LocationConnector, not the
        // unplaced-bootstrap GetDistrictByNameInCityQuery fallback the other district-move tests use
        var stateId = Guid.NewGuid();
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
        // Arrange Ã¢â‚¬â€ the player is already placed in a real district connected to the destination
        // by a hub connector, exercising the normal (non-bootstrap) district-to-district move
        var stateId = Guid.NewGuid();
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
        var oldLocation = Builders.MakeLocation(WorldId, StateId);
        var newLocation = Builders.MakeLocation(WorldId, StateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: oldLocation.Id,
            state: CreatureState.Dead
        );
        var quest = Builders.MakeQuest(corpse.Id, WorldId);
        var item = Builders.MakeWeaponItem(WorldId);
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
}
