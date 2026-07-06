using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class SceneServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private Creature _nearbyCreature = null!;
    private Creature _player = null!;
    private SceneService _service = null!;
    private State _state = null!;
    private Guid _worldId;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        var jobService = new JobService(_context);
        var creatureService = new CreatureService(_context);
        var buildingService = new BuildingService(_context);
        var inventoryService = new InventoryService(_context);
        var dispatcher = new JobDispatcher(
            new SleepJobHandler(creatureService), new WorkJobHandler(creatureService),
            new IdleJobHandler(creatureService), NullLogger<JobDispatcher>.Instance);
        var lockService = new LockService(buildingService, jobService, inventoryService);
        var reputationService = new ReputationService(_context);
        var jobCatchUpService = new JobCatchUpService(jobService, creatureService, dispatcher,
            NullLogger<JobCatchUpService>.Instance);
        var sessionAccessor = new CurrentGameSessionAccessor {
            State = new GameSessionState(new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero),
                new Chat(new FakeOllamaApiClient()))
        };
        _service = new SceneService(_context, jobCatchUpService, lockService, reputationService, sessionAccessor,
            NullLogger<SceneService>.Instance);

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

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetScene_ComputesPlayerAge_FromCurrentInGameYear() {
        // Arrange
        var query = new SceneQuery(_worldId, _player.Id, new InGameDate(975, "Thawmoon", 1, "Stormday", 14));

        // Act
        var result = await _service.GetScene(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(25, result.Player.Age);
    }

    [Fact]
    public async Task GetScene_ComputesNearbyCreatureAge_FromCurrentInGameYear() {
        // Arrange
        var query = new SceneQuery(_worldId, _player.Id, new InGameDate(975, "Thawmoon", 1, "Stormday", 14));

        // Act
        var result = await _service.GetScene(query, TestContext.Current.CancellationToken);

        // Assert
        var nearby = Assert.Single(result.NearbyPeople, p => p.Name == _nearbyCreature.Name);
        Assert.Equal(75, nearby.Age);
    }

    [Fact]
    public async Task GetScene_ReturnsCurrentDate_FromQuery() {
        // Arrange
        var currentDate = new InGameDate(975, "Thawmoon", 14, "Stormday", 21);
        var query = new SceneQuery(_worldId, _player.Id, currentDate);

        // Act
        var result = await _service.GetScene(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(currentDate, result.CurrentDate);
    }
}
