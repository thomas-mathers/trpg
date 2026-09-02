using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

[Collection("Database")]
public sealed class GetTradeWorkstationByLocationIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetTradeWorkstationByLocationIdsQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetTradeWorkstationByLocationIdsQueryHandler>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTradeWorkstation_WhenLocationIsIncluded()
    {
        var locationId = Guid.NewGuid();
        var tradeWorkstation = Builders.MakeWorkstation(WorldId, locationId: locationId);
        var craftingWorkstation = new Workstation
        {
            WorldId = WorldId,
            LocationId = locationId,
            Name = "Forge",
            Description = "A test forge",
            WorkstationType = WorkstationType.Weaponsmithing,
        };
        var workstationAtOtherLocation = Builders.MakeWorkstation(
            WorldId,
            locationId: Guid.NewGuid()
        );
        _context.Props.AddRange(tradeWorkstation, craftingWorkstation, workstationAtOtherLocation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTradeWorkstationByLocationIdsQuery { LocationIds = [locationId] },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(tradeWorkstation.Id, result.Id);
    }
}
