using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Commands;

[Collection("Database")]
public sealed class RemoveBuildingOwnerCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private TrpgDbContext _context = null!;
    private GetAllBuildingOwnersByBuildingIdQueryHandler _getAllBuildingOwnersByBuildingId = null!;
    private RemoveBuildingOwnerCommandHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new RemoveBuildingOwnerCommandHandler(_context);
        _addBuildingOwner = new AddBuildingOwnerCommandHandler(_context);
        _getAllBuildingOwnersByBuildingId = new GetAllBuildingOwnersByBuildingIdQueryHandler(
            _context
        );

        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RemovesOwnership()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = ownerId },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new RemoveBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = ownerId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var owners = await _getAllBuildingOwnersByBuildingId.Handle(
            new GetAllBuildingOwnersByBuildingIdQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Empty(owners);
    }
}
