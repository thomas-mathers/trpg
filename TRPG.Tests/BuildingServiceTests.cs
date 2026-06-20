using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class BuildingServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private BuildingService _service = null!;
    private Building _building = null!;
    private readonly Guid _cityId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new BuildingService(_context);

        _building = Builders.MakeBuilding(_cityId);
        _context.Buildings.Add(_building);
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
    public async Task GetById_ReturnsBuilding_WhenExists()
    {
        // Act
        var result = await _service.GetById(_building.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_building.Id, result.Id);
    }

    [Fact]
    public async Task GetAllByCityId_ReturnsBuildingsInCity()
    {
        // Act
        var result = await _service.GetAllByCityId(_cityId);

        // Assert
        Assert.Contains(result, b => b.Id == _building.Id);
        Assert.All(result, b => Assert.Equal(_cityId, b.CityId));
    }

    [Fact]
    public async Task AddOwner_CreatesOwnership()
    {
        // Act
        await _service.AddOwner(_building.Id, _ownerId);

        // Assert
        var owners = await _service.GetAllOwnersByBuildingId(_building.Id);
        Assert.Single(owners);
        Assert.Equal(_ownerId, owners[0].OwnerId);
    }

    [Fact]
    public async Task GetAllOwnersByBuildingId_ReturnsOwners()
    {
        // Arrange
        var ownerId2 = Guid.NewGuid();
        await _service.AddOwner(_building.Id, _ownerId);
        await _service.AddOwner(_building.Id, ownerId2);

        // Act
        var result = await _service.GetAllOwnersByBuildingId(_building.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.OwnerId == _ownerId);
        Assert.Contains(result, o => o.OwnerId == ownerId2);
    }

    [Fact]
    public async Task GetAllPropsByBuildingId_ReturnsProps()
    {
        // Arrange
        var prop = new BuildingProp
        {
            BuildingId = _building.Id,
            Name = $"Prop-{Guid.NewGuid():N}",
            Description = "A test prop",
            Coordinates = new Point(1, 1)
        };
        _context.BuildingProps.Add(prop);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllPropsByBuildingId(_building.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(prop.Id, result[0].Id);
    }

    [Fact]
    public async Task RemoveOwner_RemovesOwnership()
    {
        // Arrange
        await _service.AddOwner(_building.Id, _ownerId);

        // Act
        await _service.RemoveOwner(_building.Id, _ownerId);

        // Assert
        var owners = await _service.GetAllOwnersByBuildingId(_building.Id);
        Assert.Empty(owners);
    }
}
