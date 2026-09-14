using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

public sealed class GetRoomsByBuildingIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetRoomsByBuildingIdsQueryHandler _handler = null!;
    private readonly Building _buildingA = Builders.MakeBuilding(worldId: WorldId);
    private readonly Building _buildingB = Builders.MakeBuilding(worldId: WorldId);
    private readonly Building _otherBuilding = Builders.MakeBuilding(worldId: WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetRoomsByBuildingIdsQueryHandler>();

        _context.Buildings.AddRange(_buildingA, _buildingB, _otherBuilding);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsRoomsAcrossAllRequestedBuildings_ExcludingOthers()
    {
        // Arrange
        var roomInA = Builders.MakeRoom(_buildingA.Id, worldId: WorldId);
        var roomInB = Builders.MakeRoom(_buildingB.Id, worldId: WorldId);
        var roomInOther = Builders.MakeRoom(_otherBuilding.Id, worldId: WorldId);
        _context.Rooms.AddRange(roomInA, roomInB, roomInOther);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetRoomsByBuildingIdsQuery { BuildingIds = [_buildingA.Id, _buildingB.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { roomInA.Id, roomInB.Id }.OrderBy(id => id),
            result.Select(room => room.Id).OrderBy(id => id)
        );
    }
}
