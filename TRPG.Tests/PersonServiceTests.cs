using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class PersonServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private Person _person = null!;
    private PersonService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new PersonService(_context);

        _person = Builders.MakePerson();
        _context.Persons.Add(_person);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Add_PersistsPerson() {
        // Arrange
        var person = Builders.MakePerson();

        // Act
        await _service.Add(person, TestContext.Current.CancellationToken);

        // Assert
        var found = await _context.Persons.FindAsync([person.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Equal(person.Name, found.Name);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsPerson_WhenExists() {
        // Act
        var result = await _service.GetById(_person.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_person.Id, result.Id);
    }

    [Fact]
    public async Task Update_SavesChanges() {
        // Arrange
        _person.Gold = 500;

        // Act
        await _service.Update(_person, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Persons.FindAsync([_person.Id], TestContext.Current.CancellationToken);
        Assert.Equal(500, updated!.Gold);
    }

    [Fact]
    public async Task Delete_RemovesPerson() {
        // Arrange
        var person = Builders.MakePerson();
        await _service.Add(person, TestContext.Current.CancellationToken);

        // Act
        await _service.Delete(person.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var found = await verifyContext.Persons.FindAsync([person.Id], TestContext.Current.CancellationToken);
        Assert.Null(found);
    }

    [Fact]
    public async Task GetAllInState_ReturnsOnlyPersonsInState() {
        // Arrange
        var worldId = Guid.NewGuid();
        var stateId = Guid.NewGuid();

        var inState = Builders.MakePerson(worldId, stateId: stateId);
        var differentState = Builders.MakePerson(worldId);
        var otherWorld = Builders.MakePerson(stateId: stateId);

        await _service.Add(inState, TestContext.Current.CancellationToken);
        await _service.Add(differentState, TestContext.Current.CancellationToken);
        await _service.Add(otherWorld, TestContext.Current.CancellationToken);

        // Act
        var results = await _service.GetAllInState(worldId, stateId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(results, p => p.Id == inState.Id);
        Assert.DoesNotContain(results, p => p.Id == differentState.Id);
        Assert.DoesNotContain(results, p => p.Id == otherWorld.Id);
    }

    [Fact]
    public async Task GetIdsByDistrict_ReturnsOnlyPersonsInDistrict() {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        var inDistrict = Builders.MakePerson(worldId, districtId: districtId);
        var differentDistrict = Builders.MakePerson(worldId, districtId: Guid.NewGuid());
        var otherWorld = Builders.MakePerson(districtId: districtId);

        await _service.Add(inDistrict, TestContext.Current.CancellationToken);
        await _service.Add(differentDistrict, TestContext.Current.CancellationToken);
        await _service.Add(otherWorld, TestContext.Current.CancellationToken);

        // Act
        var results = await _service.GetIdsByDistrict(worldId, districtId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(inDistrict.Id, results);
        Assert.DoesNotContain(differentDistrict.Id, results);
        Assert.DoesNotContain(otherWorld.Id, results);
    }

    [Fact]
    public async Task GetByNameOutdoorsInDistrict_ReturnsPerson_WhenInDistrict() {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var person = Builders.MakePerson(worldId, districtId: districtId);
        await _service.Add(person, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetByNameOutdoorsInDistrict(worldId, districtId, person.Name,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(person.Id, result.Id);
    }

    [Fact]
    public async Task GetByNameOutdoorsInDistrict_ReturnsNull_WhenInDifferentDistrict() {
        // Arrange
        var worldId = Guid.NewGuid();
        var person = Builders.MakePerson(worldId, districtId: Guid.NewGuid());
        await _service.Add(person, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetByNameOutdoorsInDistrict(worldId, Guid.NewGuid(), person.Name,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByNameNearby_ReturnsPerson_WhenIndoors() {
        // Arrange
        var worldId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var target = Builders.MakePerson(worldId);
        target.RoomId = roomId;
        await _service.Add(target, TestContext.Current.CancellationToken);
        var player = Builders.MakePerson(worldId);
        player.RoomId = roomId;

        // Act
        var result = await _service.GetByNameNearby(worldId, player, target.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    [Fact]
    public async Task GetByNameNearby_ScopesToDistrict_WhenOutdoorsInCity() {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var target = Builders.MakePerson(worldId, districtId: districtId);
        await _service.Add(target, TestContext.Current.CancellationToken);
        var player = Builders.MakePerson(worldId, districtId: districtId);

        // Act
        var result = await _service.GetByNameNearby(worldId, player, target.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    [Fact]
    public async Task GetByNameNearby_ScopesToState_WhenOutdoorsWithNoCity() {
        // Arrange
        var worldId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var target = Builders.MakePerson(worldId, stateId: stateId);
        await _service.Add(target, TestContext.Current.CancellationToken);
        var player = Builders.MakePerson(worldId, stateId: stateId);

        // Act
        var result = await _service.GetByNameNearby(worldId, player, target.Name, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }
}