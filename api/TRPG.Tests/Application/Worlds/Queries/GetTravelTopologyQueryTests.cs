using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

public sealed class GetTravelTopologyQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private GetTravelTopologyQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetTravelTopologyQueryHandler(_context);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsMeasuredEdgesForAllRequestedWorlds()
    {
        var firstWorldId = Guid.NewGuid();
        var secondWorldId = Guid.NewGuid();
        var first = Builders.MakeLocationConnector(Guid.NewGuid(), Guid.NewGuid(), firstWorldId);
        var second = Builders.MakeLocationConnector(Guid.NewGuid(), Guid.NewGuid(), secondWorldId);
        _context.LocationConnectors.AddRange(first, second);
        _context.TravelConnectors.AddRange(
            Builders.MakeTravelConnector(first.Id, distance: 3, worldId: firstWorldId),
            Builders.MakeTravelConnector(second.Id, distance: 5, worldId: secondWorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTravelTopologyQuery { WorldIds = [firstWorldId, secondWorldId] },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.Count);
        Assert.Contains(
            result,
            edge =>
                edge.ConnectorId == first.Id
                && edge.OriginLocationId == first.OriginLocationId
                && edge.DestinationLocationId == first.DestinationLocationId
                && edge.Distance == 3
        );
        Assert.Contains(result, edge => edge.ConnectorId == second.Id && edge.Distance == 5);
    }
}
