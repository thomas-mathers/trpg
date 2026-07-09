using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Queries;

[Collection("Database")]
public sealed class GetBuildingByIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private Building _building = null!;
    private TrpgDbContext _context = null!;
    private GetBuildingByIdQueryHandler _handler = null!;
    private readonly Guid _stateId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetBuildingByIdQueryHandler(
            _context,
            new MemoryCache(new MemoryCacheOptions())
        );

        _building = Builders.MakeBuilding(_stateId);
        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _handler.Handle(
            new GetBuildingByIdQuery { Id = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsBuilding_WhenExists()
    {
        // Act
        var result = await _handler.Handle(
            new GetBuildingByIdQuery { Id = _building.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_building.Id, result.Id);
    }
}
