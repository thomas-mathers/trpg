using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

public sealed class GetBuildingIdByLocationIdQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetBuildingIdByLocationIdQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetBuildingIdByLocationIdQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ResolvesTheBuildingOwningTheRoomAtThatLocation()
    {
        // Arrange
        var building = Builders.MakeBuilding(worldId: WorldId);
        var roomId = Guid.NewGuid();
        var roomLocation = Builders.MakeLocation(WorldId, roomId: roomId);
        var room = Builders.MakeRoom(
            building.Id,
            id: roomId,
            worldId: WorldId,
            locationId: roomLocation.Id
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(roomLocation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetBuildingIdByLocationIdQuery { LocationId = roomLocation.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(building.Id, result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheLocationHasNoRoom()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetBuildingIdByLocationIdQuery { LocationId = location.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
