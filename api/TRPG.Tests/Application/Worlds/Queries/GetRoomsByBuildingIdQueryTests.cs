using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetRoomsByBuildingIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetRoomsByBuildingIdQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding(worldId: WorldId);
    private readonly Building _otherBuilding = Builders.MakeBuilding(worldId: WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetRoomsByBuildingIdQueryHandler>();

        _context.Buildings.AddRange(_building, _otherBuilding);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyRoomsInTheRequestedBuilding()
    {
        // Arrange
        var roomInBuilding = Builders.MakeRoom(_building.Id, worldId: WorldId);
        var roomInOtherBuilding = Builders.MakeRoom(_otherBuilding.Id, worldId: WorldId);
        _context.Rooms.AddRange(roomInBuilding, roomInOtherBuilding);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetRoomsByBuildingIdQuery { BuildingId = _building.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var room = Assert.Single(result);
        Assert.Equal(roomInBuilding.Id, room.Id);
    }
}
