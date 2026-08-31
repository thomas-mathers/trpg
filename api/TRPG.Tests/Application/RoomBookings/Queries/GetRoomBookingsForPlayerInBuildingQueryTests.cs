using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.RoomBookings.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.RoomBookings.Queries;

[Collection("Database")]
public sealed class GetRoomBookingsForPlayerInBuildingQueryTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetRoomBookingsForPlayerInBuildingQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding(worldId: WorldId);
    private readonly Building _otherBuilding = Builders.MakeBuilding(worldId: WorldId);
    private readonly Guid _playerId = Guid.NewGuid();
    private Room _room = null!;
    private Room _otherBuildingRoom = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetRoomBookingsForPlayerInBuildingQueryHandler>();

        _room = Builders.MakeRoom(_building.Id, worldId: WorldId);
        _otherBuildingRoom = Builders.MakeRoom(_otherBuilding.Id, worldId: WorldId);
        _context.Buildings.AddRange(_building, _otherBuilding);
        _context.Rooms.AddRange(_room, _otherBuildingRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyBookingsForThePlayerInThatBuilding()
    {
        // Arrange
        var matchingBooking = new RoomBooking
        {
            WorldId = WorldId,
            RoomId = _room.Id,
            KeyItemId = Guid.NewGuid(),
            PlayerId = _playerId,
            DueAtPlaytime = TimeSpan.FromHours(24),
        };
        var otherBuildingBooking = new RoomBooking
        {
            WorldId = WorldId,
            RoomId = _otherBuildingRoom.Id,
            KeyItemId = Guid.NewGuid(),
            PlayerId = _playerId,
            DueAtPlaytime = TimeSpan.FromHours(24),
        };
        var otherPlayerBooking = new RoomBooking
        {
            WorldId = WorldId,
            RoomId = _room.Id,
            KeyItemId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            DueAtPlaytime = TimeSpan.FromHours(24),
        };
        _context.RoomBookings.AddRange(matchingBooking, otherBuildingBooking, otherPlayerBooking);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetRoomBookingsForPlayerInBuildingQuery
            {
                PlayerId = _playerId,
                BuildingId = _building.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var booking = Assert.Single(result);
        Assert.Equal(matchingBooking.Id, booking.Id);
    }
}
