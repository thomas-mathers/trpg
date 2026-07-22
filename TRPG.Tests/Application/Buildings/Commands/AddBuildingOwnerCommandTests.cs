using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Buildings.Commands;

[Collection("Database")]
public sealed class AddBuildingOwnerCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid StateId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetAllOwnersByBuildingIdQueryHandler _getAllOwnersByBuildingId = null!;
    private AddBuildingOwnerCommandHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding(StateId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AddBuildingOwnerCommandHandler(_context);
        _getAllOwnersByBuildingId = new GetAllOwnersByBuildingIdQueryHandler(_context);

        await _context.AddBuilding(_building, TestContext.Current.CancellationToken);
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
