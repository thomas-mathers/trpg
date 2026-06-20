using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class ProfessionServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ProfessionService _service = null!;
    private Profession _profession = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new ProfessionService(_context);

        _profession = Builders.MakeProfession();
        _context.Professions.Add(_profession);
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
    public async Task GetById_ReturnsProfession_WhenExists()
    {
        // Act
        var result = await _service.GetById(_profession.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_profession.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ReturnsAllProfessions()
    {
        // Act
        var result = await _service.GetAll();

        // Assert
        Assert.Contains(result, p => p.Id == _profession.Id);
    }
}
