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
        Assert.Equal(entranceRoom.Id, result.Player.RoomId);
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
        var frontDoor = new RoomConnector
        {
            RoomId = entranceRoom.Id,
            Name = "Front Door",
            Description = "The door leading outside.",
            DestinationRoomId = null,
            IsLocked = true,
        };
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
        // Arrange
        var stateId = Guid.NewGuid();
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: WorldId);
        var district = Builders.MakeDistrict(city.Id, worldId: WorldId, name: "Market Row");
        var player = Builders.MakeCreature(WorldId, stateId: stateId, cityId: city.Id);
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
        Assert.Equal(district.Id, result.Player.DistrictId);
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
        var currentRoom = Builders.MakeRoom(building.Id);
        var nextRoom = Builders.MakeRoom(building.Id, capacity: 4);
        var connector = new RoomConnector
        {
            RoomId = currentRoom.Id,
            Name = "Hallway",
            Description = "A hallway.",
            DestinationRoomId = nextRoom.Id,
            IsLocked = false,
        };
        var player = Builders.MakeCreature(WorldId, stateId: StateId, roomId: currentRoom.Id);
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(currentRoom, nextRoom);
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
        Assert.Equal(nextRoom.Id, result.Player.RoomId);
    }

    [Fact]
    public async Task Handle_ReturnsExitNotFound_WhenIndoorsAndNoExitMatches()
    {
        // Arrange
        var building = Builders.MakeBuilding(StateId);
        var currentRoom = Builders.MakeRoom(building.Id);
        var player = Builders.MakeCreature(WorldId, stateId: StateId, roomId: currentRoom.Id);
        _context.Buildings.Add(building);
        _context.Rooms.Add(currentRoom);
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
        var player = Builders.MakeCreature(WorldId, stateId: stateId, cityId: city.Id);
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
        Assert.Equal(district.Id, updated!.DistrictId);
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
            cityId: city.Id,
            districtId: oldDistrict.Id
        );
        var corpse = Builders.MakeCreature(
            WorldId,
            stateId: stateId,
            cityId: city.Id,
            districtId: oldDistrict.Id,
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
