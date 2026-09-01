using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

[Collection("Database")]
public sealed class RemoveBuildingOwnerCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private TrpgDbContext _context = null!;
    private GetBuildingOwnersByBuildingIdQueryHandler _getAllBuildingOwnersByBuildingId = null!;
    private RemoveBuildingOwnerCommandHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new RemoveBuildingOwnerCommandHandler(_context);
        _addBuildingOwner = new AddBuildingOwnerCommandHandler(_context);
        _getAllBuildingOwnersByBuildingId = new GetBuildingOwnersByBuildingIdQueryHandler(_context);

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
            new GetBuildingOwnersByBuildingIdQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Empty(owners);
    }
}
