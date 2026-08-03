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

        _player = Builders.MakeCreature(WorldId, stateId: _state.Id, birthYear: 950);
        _nearbyCreature = Builders.MakeCreature(WorldId, stateId: _state.Id, birthYear: 900);

        _context.Countries.Add(country);
        _context.States.Add(_state);
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

        // Assert — the wire date mirrors the in-game date, minus the internal DayOfWeek
        Assert.Equal(new SceneDateInfo(975, "Thawmoon", 14, "Stormday", 21), result.CurrentDate);
    }

    [Fact]
    public async Task Handle_ReturnsRoomAndExitToDestinationName_WhenIndoors()
    {
        // Arrange
        var building = Builders.MakeBuilding(_state.Id);
        var room = Builders.MakeRoom(building.Id, worldId: WorldId);
        var destinationRoom = Builders.MakeRoom(building.Id, worldId: WorldId);
        var connector = Builders.MakeRoomConnector(
            room.Id,
            destinationRoomId: destinationRoom.Id,
            worldId: WorldId,
            name: "Wooden Door",
            description: "A creaking wooden door."
        );
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(room, destinationRoom);
        _context.Props.Add(connector);
        _player.RoomId = room.Id;
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
        // Arrange — a single skill at level 2 contributes CalculateExperienceFromSkillLevel(2) = 2
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
        // Arrange — nearby creatures never accumulate tracked skill XP the way the player does, so
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
    public async Task Handle_SeparatesDungeonsFromOrdinaryBuildings_WhenOutdoors()
    {
        // Arrange
        var shop = Builders.MakeBuilding(
            _state.Id,
            worldId: WorldId,
            buildingType: BuildingType.Blacksmith
        );
        var cave = Builders.MakeBuilding(
            _state.Id,
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
        var building = Assert.Single(result.NearbyBuildings);
        Assert.Equal(shop.Name, building.Name);
        var dungeon = Assert.Single(result.NearbyDungeons);
        Assert.Equal(cave.Name, dungeon.Name);
    }
}
