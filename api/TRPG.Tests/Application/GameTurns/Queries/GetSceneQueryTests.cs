using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Queries;

public sealed class GetSceneQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
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
            GameTime = GameClock.Epoch,
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
            GameTime = GameClock.Epoch,
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
            GameTime = GameClock.Epoch,
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
            GameTime = GameClock.Epoch,
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
            GameTime = GameClock.Epoch,
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
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert - the wire date mirrors the in-game date, minus the internal DayOfWeek
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
        // Room locations also carry their containing district's id; Kind must take priority over it.
        var destinationRoomId = Guid.NewGuid();
        var destinationLocation = Builders.MakeLocation(
            WorldId,
            _state.Id,
            districtId: Guid.NewGuid(),
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
            worldId: WorldId,
            name: "Wooden Door",
            description: "A creaking wooden door.",
            destinationLabel: destinationRoom.Name
        );
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(room, destinationRoom);
        _context.Locations.AddRange(location, destinationLocation);
        _context.LocationConnectors.Add(connector);
        _player.LocationId = room.LocationId;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch,
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
    public async Task Handle_IncludesBuildingFactionDetails_WhenIndoors()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId);
        var building = Builders.MakeBuilding(worldId: WorldId);
        building.FactionId = faction.Id;
        var roomId = Guid.NewGuid();
        var location = Builders.MakeLocation(WorldId, _state.Id, roomId: roomId);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            id: roomId,
            locationId: location.Id
        );
        _context.Factions.Add(faction);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _player.LocationId = location.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(faction.Name, result.Building!.FactionName);
        Assert.Equal(faction.Description, result.Building.FactionDescription);
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
            worldId: WorldId,
            name: "Path",
            description: "A path leading to City Center.",
            destinationLabel: cityCenter.Name
        );
        _context.Districts.Add(cityCenter);
        _context.Locations.Add(cityCenterLocation);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var exit = Assert.Single(result.Exits);
        var destination = Assert.IsType<SceneDistrictExitDestination>(exit.Destination);
        Assert.Equal("City Center", destination.Name);
        Assert.Equal(TRPG.Domain.Models.DistrictType.CityCenter, destination.DistrictType);
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
            GameTime = GameClock.Epoch,
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
            GameTime = GameClock.Epoch,
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
        // Arrange - a single skill at level 2 contributes CalculateExperienceFromSkillLevel(2) = 2
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
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Player.ExperienceCurrent);
        Assert.Equal(2, result.Player.ExperienceToNextLevel);
    }

    [Fact]
    public async Task Handle_DerivesExperienceProgress_FromSkillLevels_ForNearbyCreatures()
    {
        // Arrange - a creature's level is derived from its skill levels, so its progress within
        // that level has to be read from them too or it reports as negative.
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
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var nearby = Assert.Single(result.NearbyCreatures, p => p.Name == _nearbyCreature.Name);
        Assert.Equal(2, nearby.ExperienceCurrent);
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
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            new[] { shop.Name, cave.Name }.OrderBy(name => name),
            result.NearbyBuildings.Select(b => b.Name).OrderBy(name => name)
        );
    }

    [Fact]
    public async Task Handle_IncludesProps_WhenOutdoors()
    {
        // Arrange
        var sign = Builders.MakeSign(
            worldId: WorldId,
            locationId: _player.LocationId,
            text: "Caravan schedule:\nClockwise: every 1 day at 07:00"
        );
        _context.Props.Add(sign);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var nearbyProp = Assert.Single(result.NearbyProps);
        Assert.Equal(sign.Name, nearbyProp.Name);
        Assert.Equal(sign.Description, nearbyProp.Description);
        Assert.Equal("Sign", nearbyProp.Type);
    }

    [Fact]
    public async Task Handle_IncludesWeather_WhenOutdoors()
    {
        // Arrange
        _context.WeatherStates.Add(
            new WeatherState
            {
                WorldId = WorldId,
                StateId = _state.Id,
                Condition = WeatherCondition.Storm,
                NextChangeGameTime = GameClock.Epoch + TimeSpan.FromHours(10),
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WeatherCondition.Storm, result.Weather);
    }

    [Fact]
    public async Task Handle_OmitsWeather_WhenIndoors()
    {
        // Arrange
        _context.WeatherStates.Add(
            new WeatherState
            {
                WorldId = WorldId,
                StateId = _state.Id,
                Condition = WeatherCondition.Storm,
                NextChangeGameTime = GameClock.Epoch + TimeSpan.FromHours(10),
            }
        );
        var roomId = Guid.NewGuid();
        var location = Builders.MakeLocation(WorldId, _state.Id, roomId: roomId);
        var building = Builders.MakeBuilding();
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            id: roomId,
            locationId: location.Id
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _player.LocationId = location.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result.Weather);
    }

    [Fact]
    public async Task Handle_ExcludesBuildingEntranceFromExits_WhenOutdoors()
    {
        // Arrange
        var building = Builders.MakeBuilding(
            exteriorLocationId: _player.LocationId,
            worldId: WorldId
        );
        var entranceRoomId = Guid.NewGuid();
        var entranceLocation = Builders.MakeLocation(WorldId, _state.Id, roomId: entranceRoomId);
        var entranceRoom = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            id: entranceRoomId,
            locationId: entranceLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            _player.LocationId,
            destinationLocationId: entranceRoom.LocationId,
            worldId: WorldId,
            name: "Front Door",
            description: $"The door leading into {building.Name}.",
            destinationLabel: building.Name
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(entranceRoom);
        _context.Locations.Add(entranceLocation);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.NearbyBuildings, b => b.Name == building.Name);
        Assert.Empty(result.Exits);
    }

    [Fact]
    public async Task Handle_IncludesTheCaravan_WhenItIsLingeringAtThePlayerLocation()
    {
        // Arrange
        var destinationLocationId = Guid.NewGuid();
        var caravan = await SeedCaravanAtPlayerLocation(destinationLocationId);
        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var caravanInfo = Assert.Single(result.NearbyCaravans);
        Assert.Equal(caravan.Id, caravanInfo.CaravanId);
        Assert.Equal(15, caravanInfo.TicketFeeGold);
        var destination = Assert.Single(caravanInfo.Destinations);
        Assert.Equal(destinationLocationId, destination.LocationId);
        Assert.Equal("Faraway City", destination.LocationName);
        Assert.False(destination.HasTicket);
    }

    [Fact]
    public async Task Handle_ExcludesTheCaravan_WhenItIsInTransit()
    {
        // Arrange
        await SeedCaravanAtPlayerLocation(Guid.NewGuid());
        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 2,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.NearbyCaravans);
    }

    [Fact]
    public async Task Handle_StillIncludesTheCaravan_WhenThePlayerHoldsATicketDespiteTimeDrift()
    {
        // Arrange — the live position looks departed (same elapsed time as the "excludes" case
        // above), but the player bought a ticket while it was actually here and never left.
        var destinationLocationId = Guid.NewGuid();
        var caravan = await SeedCaravanAtPlayerLocation(destinationLocationId);
        _context.CaravanTickets.Add(
            Builders.MakeCaravanTicket(
                caravan.Id,
                _player.Id,
                _player.LocationId,
                destinationLocationId,
                purchasedAtGameTime: GameClock.Epoch
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var query = new GetSceneQuery
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CurrentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, 14),
            GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 2,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var caravanInfo = Assert.Single(result.NearbyCaravans);
        Assert.Equal(caravan.Id, caravanInfo.CaravanId);
        var destination = Assert.Single(caravanInfo.Destinations);
        Assert.True(destination.HasTicket);
    }

    private async Task<RouteTraveler> SeedCaravanAtPlayerLocation(Guid destinationLocationId)
    {
        var destinationCity = Builders.MakeCity(
            _state.Id,
            Guid.NewGuid(),
            name: "Faraway City",
            worldId: WorldId
        );
        var destinationDistrictId = Guid.NewGuid();
        var destinationLocation = Builders.MakeLocation(
            WorldId,
            _state.Id,
            districtId: destinationDistrictId,
            id: destinationLocationId
        );
        var destinationDistrict = Builders.MakeDistrict(
            destinationCity.Id,
            name: "The Outer Gate",
            worldId: WorldId,
            id: destinationDistrictId,
            locationId: destinationLocationId
        );

        var route = Builders.MakeCaravanRoute(WorldId);
        var connectorHere = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: WorldId
        );
        var connectorThere = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: WorldId
        );
        var stopHere = Builders.MakeCaravanRouteStop(
            route.Id,
            0,
            _player.LocationId,
            connectorHere.ConnectorId
        );
        var stopThere = Builders.MakeCaravanRouteStop(
            route.Id,
            1,
            destinationLocationId,
            connectorThere.ConnectorId
        );
        var fare = Builders.MakeCaravanFare(route.Id, WorldId, ticketFeeGold: 15);
        var caravan = Builders.MakeCaravan(route.Id, WorldId);

        _context.Cities.Add(destinationCity);
        _context.Locations.Add(destinationLocation);
        _context.Districts.Add(destinationDistrict);
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(stopHere, stopThere);
        _context.TravelConnectors.AddRange(connectorHere, connectorThere);
        _context.CaravanFares.Add(fare);
        _context.RouteTravelers.Add(caravan);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return caravan;
    }
}
