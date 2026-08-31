using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

[Collection("Database")]
public sealed class GetTradeWorkstationByBuildingIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetTradeWorkstationByBuildingIdQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding(worldId: WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetTradeWorkstationByBuildingIdQueryHandler>();

        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheTradeWorkstationInTheBuilding()
    {
        // Arrange
        var room = Builders.MakeRoom(_building.Id, worldId: WorldId);
        var tradeWorkstation = Builders.MakeWorkstation(WorldId, locationId: room.LocationId);
        var craftingWorkstation = new Workstation
        {
            WorldId = WorldId,
            LocationId = room.LocationId,
            Name = "Forge",
            Description = "A test forge",
            WorkstationType = WorkstationType.Weaponsmithing,
        };
        _context.Rooms.Add(room);
        _context.Props.AddRange(tradeWorkstation, craftingWorkstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetTradeWorkstationByBuildingIdQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tradeWorkstation.Id, result.Id);
    }
}
