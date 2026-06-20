using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class RaceServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private RaceService _service = null!;
    private Race _race = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new RaceService(_context);

        _race = Builders.MakeRace();
        _context.Races.Add(_race);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetById(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsRace_WhenExists()
    {
        // Act
        var result = await _service.GetById(_race.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_race.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ReturnsAllRaces()
    {
        // Act
        var result = await _service.GetAll();

        // Assert
        Assert.Contains(result, r => r.Id == _race.Id);
    }
}
