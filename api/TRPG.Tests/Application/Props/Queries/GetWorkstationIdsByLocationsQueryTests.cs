using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

public sealed class GetWorkstationIdsByLocationsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetWorkstationIdsByLocationsQueryHandler _handler = null!;
    private readonly Guid _firstLocationId = Guid.NewGuid();
    private readonly Guid _secondLocationId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetWorkstationIdsByLocationsQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyWorkstationIdsAtTheRequestedLocations()
    {
        // Arrange
        var first = Builders.MakeWorkstation(WorldId, locationId: _firstLocationId);
        var second = Builders.MakeWorkstation(WorldId, locationId: _secondLocationId);
        var elsewhere = Builders.MakeWorkstation(WorldId);
        var container = Builders.MakeContainer(WorldId, _firstLocationId);
        _context.Props.AddRange(first, second, elsewhere, container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetWorkstationIdsByLocationsQuery
            {
                LocationIds = [_firstLocationId, _secondLocationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id), result.OrderBy(id => id));
    }

    [Fact]
    public async Task Handle_ExcludesNonTradeWorkstations()
    {
        // Arrange
        var trade = Builders.MakeWorkstation(WorldId, locationId: _firstLocationId);
        var prayer = Builders.MakeWorkstation(
            WorldId,
            locationId: _firstLocationId,
            workstationType: WorkstationType.Prayer
        );
        _context.Props.AddRange(trade, prayer);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetWorkstationIdsByLocationsQuery { LocationIds = [_firstLocationId] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([trade.Id], result);
    }
}
