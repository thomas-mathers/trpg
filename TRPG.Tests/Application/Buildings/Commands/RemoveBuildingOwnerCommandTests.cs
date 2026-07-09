using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Commands;

[Collection("Database")]
public sealed class RemoveBuildingOwnerCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private Building _building = null!;
    private TrpgDbContext _context = null!;
    private GetAllOwnersByBuildingIdQueryHandler _getAllOwnersByBuildingId = null!;
    private RemoveBuildingOwnerCommandHandler _handler = null!;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new RemoveBuildingOwnerCommandHandler(_context);
        _addBuildingOwner = new AddBuildingOwnerCommandHandler(_context);
        _getAllOwnersByBuildingId = new GetAllOwnersByBuildingIdQueryHandler(_context);

        _building = Builders.MakeBuilding(_stateId);
        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RemovesOwnership()
    {
        // Arrange
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = _ownerId },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new RemoveBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = _ownerId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var owners = await _getAllOwnersByBuildingId.Handle(
            new GetAllOwnersByBuildingIdQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Empty(owners);
    }
}
