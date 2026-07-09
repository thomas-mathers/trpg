using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Queries;

[Collection("Database")]
public sealed class GetAllOwnersByBuildingIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private Building _building = null!;
    private TrpgDbContext _context = null!;
    private GetAllOwnersByBuildingIdQueryHandler _handler = null!;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllOwnersByBuildingIdQueryHandler(_context);
        _addBuildingOwner = new AddBuildingOwnerCommandHandler(_context);

        _building = Builders.MakeBuilding(_stateId);
        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOwners()
    {
        // Arrange
        var ownerId2 = Guid.NewGuid();
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = _ownerId },
            TestContext.Current.CancellationToken
        );
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = ownerId2 },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _handler.Handle(
            new GetAllOwnersByBuildingIdQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.OwnerId == _ownerId);
        Assert.Contains(result, o => o.OwnerId == ownerId2);
    }
}
