using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class LocationServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private City _city = null!;
    private TrpgDbContext _context = null!;
    private Country _country = null!;
    private LocationService _service = null!;
    private World _world = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new LocationService(_context);

        _world = Builders.MakeWorld();
        _country = Builders.MakeCountry(_world.Id);
        _city = Builders.MakeCity(_country.Id);

        _context.Worlds.Add(_world);
        _context.Countries.Add(_country);
        _context.Cities.Add(_city);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetWorldById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetWorldById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorldById_ReturnsWorld_WhenExists() {
        // Act
        var result = await _service.GetWorldById(_world.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_world.Id, result.Id);
    }

    [Fact]
    public async Task GetCountryById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetCountryById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCountryById_ReturnsCountry_WhenExists() {
        // Act
        var result = await _service.GetCountryById(_country.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_country.Id, result.Id);
    }

    [Fact]
    public async Task GetAllCountriesByWorldId_ReturnsCountriesInWorld() {
        // Act
        var result = await _service.GetAllCountriesByWorldId(_world.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, c => c.Id == _country.Id);
        Assert.All(result, c => Assert.Equal(_world.Id, c.WorldId));
    }

    [Fact]
    public async Task GetCityById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetCityById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCityById_ReturnsCity_WhenExists() {
        // Act
        var result = await _service.GetCityById(_city.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_city.Id, result.Id);
    }

    [Fact]
    public async Task GetAllCitiesByCountryId_ReturnsCitiesInCountry() {
        // Act
        var result = await _service.GetAllCitiesByCountryId(_country.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, c => c.Id == _city.Id);
        Assert.All(result, c => Assert.Equal(_country.Id, c.CountryId));
    }
}
