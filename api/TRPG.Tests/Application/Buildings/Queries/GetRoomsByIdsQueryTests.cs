using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Queries;

[Collection("Database")]
public sealed class GetRoomsByIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetRoomsByIdsQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding(worldId: WorldId);
    private Room _first = null!;
    private Room _second = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetRoomsByIdsQueryHandler>();

        _first = Builders.MakeRoom(_building.Id, worldId: WorldId);
        _second = Builders.MakeRoom(_building.Id, worldId: WorldId);
        _context.Buildings.Add(_building);
        _context.Rooms.AddRange(_first, _second);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsRequestedRoomsKeyedById()
    {
        // Act
        var result = await _handler.Handle(
            new GetRoomsByIdsQuery { Ids = [_first.Id, Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var room = Assert.Single(result);
        Assert.Equal(_first.Id, room.Key);
        Assert.Equal(_first.Name, room.Value.Name);
    }
}
