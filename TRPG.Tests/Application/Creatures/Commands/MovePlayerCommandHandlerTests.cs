using Microsoft.Extensions.DependencyInjection;
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

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<MovePlayerCommandHandler>();

        _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
        _context.GameSessions.Add(_session);
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
        var player = Builders.MakeCreature(WorldId, stateId: StateId);
        var building = Builders.MakeBuilding(StateId, name: "The Rusty Anchor");
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
    public async Task Handle_ReturnsBuildingHasNoEntrance_WhenTheBuildingHasNoRoomAtFloorZero()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, stateId: StateId);
        var building = Builders.MakeBuilding(StateId, name: "The Empty Shell");
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
        Assert.Equal(MovePlayerOutcome.BuildingHasNoEntrance, result.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsDoorLocked_WhenTheEntranceDoorIsLockedAndPlayerHasNoKey()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, stateId: StateId);
        var building = Builders.MakeBuilding(StateId, name: "The Locked Vault");
        var entranceRoom = Builders.MakeRoom(building.Id);
        var frontDoor = Builders.MakeRoomConnector(
            entranceRoom.Id,
            isLocked: true,
            name: "Front Door",
            description: "The door leading outside."
        );
        var keyItem = new Item { Name = "Vault Key", Description = "A test key." };
        _context.Creatures.Add(player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(entranceRoom);
        _context.Props.Add(frontDoor);
        _context.Items.Add(keyItem);
        _context.RoomConnectorKeys.Add(
            new RoomConnectorKey { ItemId = keyItem.Id, RoomConnectorId = frontDoor.Id }
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
        Assert.Equal(MovePlayerOutcome.DoorLocked, result.Outcome);
    }

    [Fact]
    public async Task Handle_MovesToTheDistrict_WhenOutdoorsAndNoBuildingMatchesButADistrictDoes()
    {
        // Arrange — no location on the player at all; MoveOutdoors falls back to the state's
        // single city when there's no current location to read a CityId from
        var stateId = Guid.NewGuid();
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: WorldId);
        var district = Builders.MakeDistrict(city.Id, worldId: WorldId, name: "Market Row");
        var player = Builders.MakeCreature(WorldId, stateId: stateId);
        _context.Cities.Add(city);
        _context.Districts.Add(district);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationName = "Market Row",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(district.LocationId, result.Player.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsDestinationNotFound_WhenOutdoorsAndNothingMatches()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, stateId: StateId);
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
        Assert.Equal(MovePlayerOutcome.DestinationNotFound, result.Outcome);
    }

    [Fact]
    public async Task Handle_MovesThroughTheExit_WhenIndoorsAndDestinationMatchesAnExit()
    {
        // Arrange
        var building = Builders.MakeBuilding(StateId);
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, StateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var nextRoom = Builders.MakeRoom(building.Id, capacity: 4);
        var connector = Builders.MakeRoomConnector(
            currentRoom.Id,
            destinationRoomId: nextRoom.Id,
            name: "Hallway",
            description: "A hallway."
        );
        var player = Builders.MakeCreature(
            WorldId,
            stateId: StateId,
            locationId: currentRoom.LocationId
        );
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
        _context.Locations.Add(currentLocation);
        _context.Props.Add(connector);
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
        var building = Builders.MakeBuilding(StateId);
        var currentRoomId = Guid.NewGuid();
        var currentLocation = Builders.MakeLocation(WorldId, StateId, roomId: currentRoomId);
        var currentRoom = Builders.MakeRoom(
            building.Id,
            id: currentRoomId,
            locationId: currentLocation.Id
        );
        var player = Builders.MakeCreature(
            WorldId,
            stateId: StateId,
            locationId: currentRoom.LocationId
        );
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
        Assert.Equal(MovePlayerOutcome.ExitNotFound, result.Outcome);
    }

    [Fact]
    public async Task Handle_PersistsTheNewLocation()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: WorldId);
        var district = Builders.MakeDistrict(city.Id, worldId: WorldId, name: "Market Row");
        var player = Builders.MakeCreature(WorldId, stateId: stateId);
        _context.Cities.Add(city);
        _context.Districts.Add(district);
        _context.Creatures.Add(player);
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
        var updated = await verifyContext.Creatures.FindAsync(
            [player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(district.LocationId, updated!.LocationId);
    }

    [Fact]
    public async Task Handle_DeletesDeadCreaturesLeftBehindInTheOldDistrict()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: WorldId);
        var oldDistrict = Builders.MakeDistrict(
            city.Id,
            DistrictType.Residential,
            worldId: WorldId,
            name: "Docks"
        );
        var newDistrict = Builders.MakeDistrict(city.Id, worldId: WorldId, name: "Market Row");
        var player = Builders.MakeCreature(
            WorldId,
            stateId: stateId,
            locationId: oldDistrict.LocationId
        );
        var corpse = Builders.MakeCreature(
            WorldId,
            stateId: stateId,
            locationId: oldDistrict.LocationId,
            state: CreatureState.Dead
        );
        _context.Cities.Add(city);
        _context.Districts.AddRange(oldDistrict, newDistrict);
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
}
