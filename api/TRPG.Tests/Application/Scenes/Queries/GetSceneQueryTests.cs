using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Scenes.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Queries;

[Collection("Database")]
public sealed class GetSceneQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetSceneQueryHandler _handler = null!;
    private Creature _nearbyCreature = null!;
    private Creature _player = null!;
    private State _state = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetSceneQueryHandler>();

        var country = Builders.MakeCountry(WorldId);
        _state = Builders.MakeState(country.Id);
        var sharedLocation = Builders.MakeLocation(WorldId, _state.Id);

        _player = Builders.MakeCreature(WorldId, birthYear: 950, locationId: sharedLocation.Id);
        _nearbyCreature = Builders.MakeCreature(
            WorldId,
            birthYear: 900,
            locationId: sharedLocation.Id
        );

        _context.Countries.Add(country);
        _context.States.Add(_state);
        _context.Locations.Add(sharedLocation);
        _context.Creatures.AddRange(_player, _nearbyCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ComputesPlayerAge_FromCurrentInGameYear()
    {
        // Arrange
        var query = new GetSceneQuery
        {
            WorldId = WorldId,
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
            WorldId = WorldId,
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
    public async Task Handle_IncludesTradeWorkstationId_ForNearbyWorkerAtTradeCounter()
    {
        // Arrange
        var workstation = Builders.MakeWorkstation(
            WorldId,
            _nearbyCreature.LocationId,
            _nearbyCreature.Id
        );
        _context.Props.Add(workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var nearby = Assert.Single(result.NearbyCreatures, p => p.Id == _nearbyCreature.Id);
        Assert.Equal(workstation.Id, nearby.TradeWorkstationId);
    }

    [Fact]
    public async Task Handle_ReturnsNullTradeWorkstationId_WhenNearbyCreatureIsNotAtTradeCounter()
    {
        // Arrange
        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var nearby = Assert.Single(result.NearbyCreatures, p => p.Id == _nearbyCreature.Id);
        Assert.Null(nearby.TradeWorkstationId);
    }

    [Fact]
    public async Task Handle_ReturnsNoNearbyCreatures_WhenNooneElseIsAtTheSameLocation()
    {
        // Arrange - moving the player off the shared Location isolates them from _nearbyCreature,
        // exercising BuildNearbyPeopleInfos's early return when nobody's nearby instead of running
        // the faction/reputation queries for nothing
        var isolatedLocation = Builders.MakeLocation(WorldId, _state.Id);
        _context.Locations.Add(isolatedLocation);
        _player.LocationId = isolatedLocation.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.NearbyCreatures);
    }

    [Fact]
    public async Task Handle_ReturnsCurrentDate_FromQuery()
    {
        // Arrange
        var currentDate = new InGameDate(975, "Thawmoon", 14, "Stormday", DayOfWeek.Thursday, 21);
        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = currentDate,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert â€” the wire date mirrors the in-game date, minus the internal DayOfWeek
        Assert.Equal(new SceneDateInfo(975, "Thawmoon", 14, "Stormday", 21), result.CurrentDate);
    }

    [Fact]
    public async Task Handle_ReturnsRoomAndExitToDestinationName_WhenIndoors()
    {
        // Arrange
        var building = Builders.MakeBuilding();
        var roomId = Guid.NewGuid();
        var location = Builders.MakeLocation(WorldId, _state.Id, roomId: roomId);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            id: roomId,
            locationId: location.Id
        );
        var destinationRoomId = Guid.NewGuid();
        var destinationLocation = Builders.MakeLocation(
            WorldId,
            _state.Id,
            roomId: destinationRoomId
        );
        var destinationRoom = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            id: destinationRoomId,
            locationId: destinationLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            room.LocationId,
            destinationLocationId: destinationRoom.LocationId,
            destinationType: LocationDestinationType.Room,
            worldId: WorldId,
            name: "Wooden Door",
            description: "A creaking wooden door.",
            destinationLabel: destinationRoom.Name
        );
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(room, destinationRoom);
        _context.Locations.AddRange(location, destinationLocation);
        _context.Props.Add(connector);
        _player.LocationId = room.LocationId;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(building.Name, result.Building!.Name);
        Assert.Equal(room.Name, result.Room!.Name);
        var exit = Assert.Single(result.Exits);
        var destination = Assert.IsType<SceneRoomExitDestination>(exit.Destination);
        Assert.Equal(destinationRoom.Name, destination.Name);
        Assert.Equal(building.BuildingType, destination.BuildingType);
        Assert.False(exit.IsLocked);
    }

    [Fact]
    public async Task Handle_ReturnsExitToAdjacentDistrict_WhenOutdoors()
    {
        // Arrange
        var cityCenterId = Guid.NewGuid();
        var cityCenterLocation = Builders.MakeLocation(
            WorldId,
            _state.Id,
            districtId: cityCenterId
        );
        var cityCenter = Builders.MakeDistrict(
            Guid.NewGuid(),
            worldId: WorldId,
            name: "City Center",
            id: cityCenterId,
            locationId: cityCenterLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            _player.LocationId,
            destinationLocationId: cityCenter.LocationId,
            destinationType: LocationDestinationType.District,
            worldId: WorldId,
            name: "Path",
            description: "A path leading to City Center.",
            destinationLabel: cityCenter.Name
        );
        _context.Districts.Add(cityCenter);
        _context.Locations.Add(cityCenterLocation);
        _context.Props.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var exit = Assert.Single(result.Exits);
        var destination = Assert.IsType<SceneDistrictExitDestination>(exit.Destination);
        Assert.Equal("City Center", destination.Name);
        Assert.Equal(Data.Models.DistrictType.CityCenter, destination.DistrictType);
    }

    [Fact]
    public async Task Handle_ReturnsCurrentAndMaximumHp_ForPlayerAndNearbyPeople()
    {
        // Arrange
        _player.CurrentHp = 12;
        _nearbyCreature.CurrentHp = 7;
        // Simulate gear-boosted cached Maximum diverging from base Attributes.MaximumHp,
        // to prove the query reads the cached column rather than the base value.
        _player.MaximumHp += 50;
        _nearbyCreature.MaximumHp += 25;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(12, result.Player.CurrentHp);
        Assert.Equal(_player.MaximumHp, result.Player.MaximumHp);
        Assert.NotEqual(_player.BaseAttributes.MaximumHp, result.Player.MaximumHp);
        var nearby = Assert.Single(result.NearbyCreatures, p => p.Name == _nearbyCreature.Name);
        Assert.Equal(7, nearby.CurrentHp);
        Assert.Equal(_nearbyCreature.MaximumHp, nearby.MaximumHp);
        Assert.NotEqual(_nearbyCreature.BaseAttributes.MaximumHp, nearby.MaximumHp);
    }

    [Fact]
    public async Task Handle_ReturnsNullProfession_ForPlayerAndNearbyCreatureWithNoProfession()
    {
        // Arrange
        _player.Profession = null;
        _nearbyCreature.Profession = null;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result.Player.Profession);
        var nearby = Assert.Single(result.NearbyCreatures, p => p.Name == _nearbyCreature.Name);
        Assert.Null(nearby.Profession);
    }

    [Fact]
    public async Task Handle_ComputesExperienceProgress_ForThePlayer()
    {
        // Arrange â€” a single skill at level 2 contributes CalculateExperienceFromSkillLevel(2) = 2
        // toward character level. Level 1 floor is CalculateExperienceFromLevel(1) = 0, next level
        // floor is CalculateExperienceFromLevel(2) = 2, so this sits at Current = 2, ToNextLevel = 2.
        _player.Level = 1;
        _context.CreatureSkills.Add(
            new CreatureSkill
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                Skill = Skill.General,
                Level = 2,
                Experience = 0,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Player.ExperienceCurrent);
        Assert.Equal(2, result.Player.ExperienceToNextLevel);
    }

    [Fact]
    public async Task Handle_UsesZeroExperienceProgress_ForNearbyCreatures_RegardlessOfSkillLevels()
    {
        // Arrange â€” nearby creatures never accumulate tracked skill XP the way the player does, so
        // GetSceneQueryHandler doesn't query for it at all; a skill row here should have no effect.
        _nearbyCreature.Level = 1;
        _context.CreatureSkills.Add(
            new CreatureSkill
            {
                WorldId = WorldId,
                CreatureId = _nearbyCreature.Id,
                Skill = Skill.General,
                Level = 2,
                Experience = 0,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var nearby = Assert.Single(result.NearbyCreatures, p => p.Name == _nearbyCreature.Name);
        Assert.Equal(0, nearby.ExperienceCurrent);
    }

    [Fact]
    public async Task Handle_IncludesDungeonsAlongsideOrdinaryBuildings_WhenOutdoors()
    {
        // Arrange
        var shop = Builders.MakeBuilding(
            exteriorLocationId: _player.LocationId,
            worldId: WorldId,
            buildingType: BuildingType.Blacksmith
        );
        var cave = Builders.MakeBuilding(
            exteriorLocationId: _player.LocationId,
            worldId: WorldId,
            buildingType: BuildingType.Cave
        );
        _context.Buildings.AddRange(shop, cave);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            new[] { shop.Name, cave.Name }.OrderBy(name => name),
            result.NearbyBuildings.Select(b => b.Name).OrderBy(name => name)
        );
    }
}
