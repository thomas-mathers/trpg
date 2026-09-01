using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetBuildingOwnersByBuildingIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private TrpgDbContext _context = null!;
    private GetBuildingOwnersByBuildingIdQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetBuildingOwnersByBuildingIdQueryHandler(_context);
        _addBuildingOwner = new AddBuildingOwnerCommandHandler(_context);

        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOwners()
    {
        // Arrange
        var ownerId1 = Guid.NewGuid();
        var ownerId2 = Guid.NewGuid();
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = ownerId1 },
            TestContext.Current.CancellationToken
        );
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = _building.Id, OwnerId = ownerId2 },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _handler.Handle(
            new GetBuildingOwnersByBuildingIdQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.OwnerId == ownerId1);
        Assert.Contains(result, o => o.OwnerId == ownerId2);
    }
}
