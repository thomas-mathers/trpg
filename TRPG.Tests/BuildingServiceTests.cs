using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class BuildingServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private Building _building = null!;
    private TrpgDbContext _context = null!;
    private BuildingService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new BuildingService(_context);

        _building = Builders.MakeBuilding(_stateId);
        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsBuilding_WhenExists() {
        // Act
        var result = await _service.GetById(_building.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_building.Id, result.Id);
    }

    [Fact]
    public async Task GetAllByStateId_ReturnsBuildingsInState() {
        // Act
        var result = await _service.GetAllByStateId(_stateId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, b => b.Id == _building.Id);
        Assert.All(result, b => Assert.Equal(_stateId, b.StateId));
    }

    [Fact]
    public async Task AddOwner_CreatesOwnership() {
        // Act
        await _service.AddOwner(_building.Id, _ownerId, TestContext.Current.CancellationToken);

        // Assert
        var owners = await _service.GetAllOwnersByBuildingId(_building.Id, TestContext.Current.CancellationToken);
        Assert.Single(owners);
        Assert.Equal(_ownerId, owners.First().OwnerId);
    }

    [Fact]
    public async Task GetAllOwnersByBuildingId_ReturnsOwners() {
        // Arrange
        var ownerId2 = Guid.NewGuid();
        await _service.AddOwner(_building.Id, _ownerId, TestContext.Current.CancellationToken);
        await _service.AddOwner(_building.Id, ownerId2, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetAllOwnersByBuildingId(_building.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.OwnerId == _ownerId);
        Assert.Contains(result, o => o.OwnerId == ownerId2);
    }

    [Fact]
    public async Task GetAllPropsByRoomId_ReturnsProps() {
        // Arrange
        var room = Builders.MakeRoom(_building.Id);
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var prop = new Seat {
            RoomId = room.Id,
            Name = $"Prop-{Guid.NewGuid():N}",
            Description = "A test prop"
        };
        _context.Props.Add(prop);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetAllPropsByRoomId(room.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(prop.Id, result.First().Id);
    }

    [Fact]
    public async Task RemoveOwner_RemovesOwnership() {
        // Arrange
        await _service.AddOwner(_building.Id, _ownerId, TestContext.Current.CancellationToken);

        // Act
        await _service.RemoveOwner(_building.Id, _ownerId, TestContext.Current.CancellationToken);

        // Assert
        var owners = await _service.GetAllOwnersByBuildingId(_building.Id, TestContext.Current.CancellationToken);
        Assert.Empty(owners);
    }
}
