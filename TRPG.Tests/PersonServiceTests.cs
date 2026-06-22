using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class PersonServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private PersonService _service = null!;
    private Person _person = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new PersonService(_context);

        _person = Builders.MakePerson();
        _context.Persons.Add(_person);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Add_PersistsPerson()
    {
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
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsPerson_WhenExists()
    {
        // Act
        var result = await _service.GetById(_person.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_person.Id, result.Id);
    }

    [Fact]
    public async Task Update_SavesChanges()
    {
        // Arrange
        _person.Gold = 500;

        // Act
        await _service.Update(_person, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Persons.FindAsync([_person.Id], TestContext.Current.CancellationToken);
        Assert.Equal(500, updated!.Gold);
    }

    [Fact]
    public async Task Delete_RemovesPerson()
    {
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
    public async Task GetAllWithinRange_ReturnsOnlyPersonsInSameWorldWithinRadius()
    {
        // Arrange
        var worldId = Guid.NewGuid();

        var near = Builders.MakePerson(worldId: worldId);
        near.Location.Coordinates = new Point(1, 1);

        var far = Builders.MakePerson(worldId: worldId);
        far.Location.Coordinates = new Point(1000, 1000);

        var otherWorld = Builders.MakePerson(worldId: Guid.NewGuid());
        otherWorld.Location.Coordinates = new Point(0, 0);

        await _service.Add(near, TestContext.Current.CancellationToken);
        await _service.Add(far, TestContext.Current.CancellationToken);
        await _service.Add(otherWorld, TestContext.Current.CancellationToken);

        // Act
        var results = await _service.GetAllWithinRange(worldId, new Point(0, 0), 10f, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(results, p => p.Id == near.Id);
        Assert.DoesNotContain(results, p => p.Id == far.Id);
        Assert.DoesNotContain(results, p => p.Id == otherWorld.Id);
    }
}
