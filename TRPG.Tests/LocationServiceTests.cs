using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class LocationServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private LocationService _service = null!;
    private World _world = null!;
    private Country _country = null!;
    private Province _province = null!;
    private City _city = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new LocationService(_context);

        _world = Builders.MakeWorld();
        _country = Builders.MakeCountry(_world.Id);
        _province = Builders.MakeProvince(_country.Id);
        _city = Builders.MakeCity(_province.Id);

        _context.Worlds.Add(_world);
        _context.Countries.Add(_country);
        _context.Provinces.Add(_province);
        _context.Cities.Add(_city);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task GetWorldById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetWorldById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorldById_ReturnsWorld_WhenExists()
    {
        // Act
        var result = await _service.GetWorldById(_world.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_world.Id, result.Id);
    }

    [Fact]
    public async Task GetCountryById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetCountryById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCountryById_ReturnsCountry_WhenExists()
    {
        // Act
        var result = await _service.GetCountryById(_country.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_country.Id, result.Id);
    }

    [Fact]
    public async Task GetAllCountriesByWorldId_ReturnsCountriesInWorld()
    {
        // Act
        var result = await _service.GetAllCountriesByWorldId(_world.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, c => c.Id == _country.Id);
        Assert.All(result, c => Assert.Equal(_world.Id, c.WorldId));
    }

    [Fact]
    public async Task GetProvinceById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetProvinceById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProvinceById_ReturnsProvince_WhenExists()
    {
        // Act
        var result = await _service.GetProvinceById(_province.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_province.Id, result.Id);
    }

    [Fact]
    public async Task GetAllProvincesByCountryId_ReturnsProvincesInCountry()
    {
        // Act
        var result = await _service.GetAllProvincesByCountryId(_country.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, p => p.Id == _province.Id);
        Assert.All(result, p => Assert.Equal(_country.Id, p.CountryId));
    }

    [Fact]
    public async Task GetCityById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetCityById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCityById_ReturnsCity_WhenExists()
    {
        // Act
        var result = await _service.GetCityById(_city.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_city.Id, result.Id);
    }

    [Fact]
    public async Task GetAllCitiesByProvinceId_ReturnsCitiesInProvince()
    {
        // Act
        var result = await _service.GetAllCitiesByProvinceId(_province.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, c => c.Id == _city.Id);
        Assert.All(result, c => Assert.Equal(_province.Id, c.ProvinceId));
    }
}
