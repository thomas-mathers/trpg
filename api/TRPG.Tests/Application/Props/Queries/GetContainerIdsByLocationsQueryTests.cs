using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

public sealed class GetContainerIdsByLocationsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetContainerIdsByLocationsQueryHandler _handler = null!;
    private readonly Guid _firstLocationId = Guid.NewGuid();
    private readonly Guid _secondLocationId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetContainerIdsByLocationsQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyContainerIdsAtTheRequestedLocations()
    {
        // Arrange
        var first = Builders.MakeContainer(WorldId, _firstLocationId);
        var second = Builders.MakeContainer(WorldId, _secondLocationId);
        var elsewhere = Builders.MakeContainer(WorldId);
        var workstation = Builders.MakeWorkstation(WorldId, locationId: _firstLocationId);
        _context.Props.AddRange(first, second, elsewhere, workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetContainerIdsByLocationsQuery
            {
                LocationIds = [_firstLocationId, _secondLocationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id), result.OrderBy(id => id));
    }
}
