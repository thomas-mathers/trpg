using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class SceneServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private Person _nearbyPerson = null!;
    private Person _player = null!;
    private Race _race = null!;
    private SceneService _service = null!;
    private State _state = null!;
    private Guid _worldId;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        var jobService = new JobService(_context);
        var personService = new PersonService(_context);
        var buildingService = new BuildingService(_context);
        var inventoryService = new InventoryService(_context);
        var dispatcher = new JobDispatcher(
            new SleepJobHandler(personService), new WorkJobHandler(personService), new IdleJobHandler(personService),
            NullLogger<JobDispatcher>.Instance);
        var lockService = new LockService(buildingService, jobService, inventoryService);
        var reputationService = new ReputationService(_context);
        _service = new SceneService(_context, new JobCatchUpService(jobService, personService, dispatcher), lockService,
            reputationService);

        _worldId = Guid.NewGuid();
        var country = Builders.MakeCountry(_worldId);
        _state = Builders.MakeState(country.Id);
        _race = Builders.MakeRace(_worldId);

        _player = Builders.MakePerson(_worldId, _race.Id, stateId: _state.Id, birthYear: 950);
        _nearbyPerson = Builders.MakePerson(_worldId, _race.Id, stateId: _state.Id, birthYear: 900);

        _context.Countries.Add(country);
        _context.States.Add(_state);
        _context.Races.Add(_race);
        _context.Persons.AddRange(_player, _nearbyPerson);
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
    public async Task GetScene_ComputesNearbyPersonAge_FromCurrentInGameYear() {
        // Arrange
        var query = new SceneQuery(_worldId, _player.Id, new InGameDate(975, "Thawmoon", 1, "Stormday", 14));

        // Act
        var result = await _service.GetScene(query, TestContext.Current.CancellationToken);

        // Assert
        var nearby = Assert.Single(result.NearbyPeople, p => p.Name == _nearbyPerson.Name);
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