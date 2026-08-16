using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Commands;

[Collection("Database")]
public sealed class AddBuildingOwnerCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetAllOwnersByBuildingIdQueryHandler _getAllOwnersByBuildingId = null!;
    private AddBuildingOwnerCommandHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AddBuildingOwnerCommandHandler(_context);
        _getAllOwnersByBuildingId = new GetAllOwnersByBuildingIdQueryHandler(_context);

        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesOwnership()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new AddBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = ownerId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var owners = await _getAllOwnersByBuildingId.Handle(
            new GetAllOwnersByBuildingIdQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Single(owners);
        Assert.Equal(ownerId, owners.First().OwnerId);
    }
}
