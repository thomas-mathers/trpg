using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class SceneServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Creature _nearbyCreature = null!;
    private Creature _player = null!;
    private SceneService _service = null!;
    private State _state = null!;
    private Guid _worldId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var locationService = new LocationService(_context, cache);
        var buildingService = new BuildingService(_context, cache);
        var creatureService = new CreatureService(_context);
        var reputationService = new ReputationService(_context);
        _service = new SceneService(
            _context,
            locationService,
            buildingService,
            creatureService,
            reputationService,
            NullLogger<SceneService>.Instance
        );

        _worldId = Guid.NewGuid();
        var country = Builders.MakeCountry(_worldId);
        _state = Builders.MakeState(country.Id);

        _player = Builders.MakeCreature(_worldId, stateId: _state.Id, birthYear: 950);
        _nearbyCreature = Builders.MakeCreature(_worldId, stateId: _state.Id, birthYear: 900);

        _context.Countries.Add(country);
        _context.States.Add(_state);
        _context.Creatures.AddRange(_player, _nearbyCreature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetScene_ComputesPlayerAge_FromCurrentInGameYear()
    {
        // Arrange
        var query = new SceneQuery(
            _worldId,
            _player.Id,
            new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14)
        );

        // Act
        var result = await _service.GetScene(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(25, result.Player.Age);
    }

    [Fact]
    public async Task GetScene_ComputesNearbyCreatureAge_FromCurrentInGameYear()
    {
        // Arrange
        var query = new SceneQuery(
            _worldId,
            _player.Id,
            new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14)
        );

        // Act
        var result = await _service.GetScene(query, TestContext.Current.CancellationToken);

        // Assert
        var nearby = Assert.Single(result.NearbyPeople, p => p.Name == _nearbyCreature.Name);
        Assert.Equal(75, nearby.Age);
    }

    [Fact]
    public async Task GetScene_ReturnsCurrentDate_FromQuery()
    {
        // Arrange
        var currentDate = new InGameDate(975, "Thawmoon", 14, "Stormday", DayOfWeek.Thursday, 21);
        var query = new SceneQuery(_worldId, _player.Id, currentDate);

        // Act
        var result = await _service.GetScene(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(currentDate, result.CurrentDate);
    }

    [Fact]
    public async Task GetScene_ReturnsRoomAndExitToDestinationName_WhenIndoors()
    {
        // Arrange
        var building = Builders.MakeBuilding(_state.Id);
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var destinationRoom = Builders.MakeRoom(building.Id, worldId: _worldId);
        var connector = new RoomConnector
        {
            RoomId = room.Id,
            WorldId = _worldId,
            Name = "Wooden Door",
            Description = "A creaking wooden door.",
            DestinationRoomId = destinationRoom.Id,
            IsLocked = false,
        };
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(room, destinationRoom);
        _context.Props.Add(connector);
        _player.RoomId = room.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new SceneQuery(
            _worldId,
            _player.Id,
            new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14)
        );

        // Act
        var result = await _service.GetScene(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(building.Name, result.Building!.Name);
        Assert.Equal(room.Name, result.Room!.Name);
        var exit = Assert.Single(result.Room.Exits);
        Assert.Equal(destinationRoom.Name, exit.DestinationRoomName);
        Assert.False(exit.IsLocked);
    }
}
