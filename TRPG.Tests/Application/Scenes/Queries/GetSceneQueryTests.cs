using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Queries;

[Collection("Database")]
public sealed class GetSceneQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetSceneQueryHandler _handler = null!;
    private Creature _nearbyCreature = null!;
    private Creature _player = null!;
    private State _state = null!;
    private Guid _worldId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var getAllNearbyCreatures = new GetAllNearbyCreaturesQueryHandler(_context);
        var getEffectiveReputations = new GetEffectiveReputationsQueryHandler(
            _context,
            NullLogger<GetEffectiveReputationsQueryHandler>.Instance
        );
        _handler = new GetSceneQueryHandler(
            _context,
            new GetStateByIdQueryHandler(_context, cache),
            new GetCityByIdQueryHandler(_context, cache),
            new GetCityByStateIdQueryHandler(_context, cache),
            new GetAllDistrictsByCityIdQueryHandler(_context, cache),
            new GetRoomSummaryQueryHandler(_context, cache),
            new GetStaticPropsByRoomIdQueryHandler(_context, cache),
            new GetConnectorsByRoomIdQueryHandler(_context),
            new GetAllBuildingsByLocationQueryHandler(_context, cache),
            new GetRoomsByIdsQueryHandler(_context, cache),
            getAllNearbyCreatures,
            getEffectiveReputations,
            NullLogger<GetSceneQueryHandler>.Instance
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
    public async Task Handle_ComputesPlayerAge_FromCurrentInGameYear()
    {
        // Arrange
        var query = new GetSceneQuery
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(25, result.Player.Age);
    }

    [Fact]
    public async Task Handle_ComputesNearbyCreatureAge_FromCurrentInGameYear()
    {
        // Arrange
        var query = new GetSceneQuery
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var nearby = Assert.Single(result.NearbyCreatures, p => p.Name == _nearbyCreature.Name);
        Assert.Equal(75, nearby.Age);
    }

    [Fact]
    public async Task Handle_ReturnsCurrentDate_FromQuery()
    {
        // Arrange
        var currentDate = new InGameDate(975, "Thawmoon", 14, "Stormday", DayOfWeek.Thursday, 21);
        var query = new GetSceneQuery
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            CurrentDate = currentDate,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert — the wire date mirrors the in-game date, minus the internal DayOfWeek
        Assert.Equal(new SceneDateInfo(975, "Thawmoon", 14, "Stormday", 21), result.CurrentDate);
    }

    [Fact]
    public async Task Handle_ReturnsRoomAndExitToDestinationName_WhenIndoors()
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

        var query = new GetSceneQuery
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(building.Name, result.Building!.Name);
        Assert.Equal(room.Name, result.Room!.Name);
        var exit = Assert.Single(result.Room.Exits);
        Assert.Equal(destinationRoom.Name, exit.DestinationRoomName);
        Assert.False(exit.IsLocked);
    }

    [Fact]
    public async Task Handle_ReturnsCurrentAndMaximumHp_ForPlayerAndNearbyPeople()
    {
        // Arrange
        _player.CurrentHp = 12;
        _nearbyCreature.CurrentHp = 7;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(12, result.Player.CurrentHp);
        Assert.Equal(_player.Attributes.MaximumHp, result.Player.MaximumHp);
        var nearby = Assert.Single(result.NearbyCreatures, p => p.Name == _nearbyCreature.Name);
        Assert.Equal(7, nearby.CurrentHp);
        Assert.Equal(_nearbyCreature.Attributes.MaximumHp, nearby.MaximumHp);
    }

    [Fact]
    public async Task Handle_SeparatesDungeonsFromOrdinaryBuildings_WhenOutdoors()
    {
        // Arrange
        var shop = Builders.MakeBuilding(
            _state.Id,
            worldId: _worldId,
            buildingType: BuildingType.Blacksmith
        );
        var cave = Builders.MakeBuilding(
            _state.Id,
            worldId: _worldId,
            buildingType: BuildingType.Cave
        );
        _context.Buildings.AddRange(shop, cave);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var building = Assert.Single(result.NearbyBuildings);
        Assert.Equal(shop.Name, building.Name);
        var dungeon = Assert.Single(result.NearbyDungeons);
        Assert.Equal(cave.Name, dungeon.Name);
    }
}
