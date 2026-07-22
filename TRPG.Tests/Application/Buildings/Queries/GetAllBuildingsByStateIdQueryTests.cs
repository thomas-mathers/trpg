using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Buildings.Queries;

[Collection("Database")]
public sealed class GetAllBuildingsByStateIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid StateId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetAllBuildingsByStateIdQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding(StateId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllBuildingsByStateIdQueryHandler(_context);

        await _context.AddBuilding(_building, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsBuildingsInState()
    {
        // Act
        var result = await _handler.Handle(
            new GetAllBuildingsByStateIdQuery { StateId = StateId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, b => b.Id == _building.Id);
        Assert.All(result, b => Assert.Equal(StateId, b.StateId));
    }
}
