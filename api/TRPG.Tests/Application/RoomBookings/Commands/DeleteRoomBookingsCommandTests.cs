using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.RoomBookings.Commands;

[Collection("Database")]
public sealed class DeleteRoomBookingsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private DeleteRoomBookingsCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<DeleteRoomBookingsCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private static RoomBooking MakeBooking() =>
        new()
        {
            WorldId = WorldId,
            RoomId = Guid.NewGuid(),
            KeyItemId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            DueAtPlaytime = TimeSpan.FromHours(24),
        };

    [Fact]
    public async Task Handle_DeletesOnlyTheRequestedBookings()
    {
        // Arrange
        var toDelete = MakeBooking();
        var toKeep = MakeBooking();
        _context.RoomBookings.AddRange(toDelete, toKeep);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new DeleteRoomBookingsCommand { RoomBookingIds = [toDelete.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.RoomBookings.AnyAsync(
                b => b.Id == toDelete.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.RoomBookings.AnyAsync(
                b => b.Id == toKeep.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ClearsTheRoomsBedAssignment_WhenTheRoomHasABed()
    {
        // Arrange
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.Inn);
        var room = Builders.MakeRoom(building.Id, worldId: WorldId);
        var location = Builders.MakeLocation(WorldId, roomId: room.Id, id: room.LocationId);
        var playerId = Guid.NewGuid();
        var bed = Builders.MakeBed(
            WorldId,
            locationId: room.LocationId,
            assignedCreatureId: playerId
        );
        var booking = new RoomBooking
        {
            WorldId = WorldId,
            RoomId = room.Id,
            KeyItemId = Guid.NewGuid(),
            PlayerId = playerId,
            DueAtPlaytime = TimeSpan.FromHours(24),
        };
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Props.Add(bed);
        _context.RoomBookings.Add(booking);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new DeleteRoomBookingsCommand { RoomBookingIds = [booking.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedBed = await verifyContext
            .Props.OfType<Bed>()
            .SingleAsync(b => b.Id == bed.Id, TestContext.Current.CancellationToken);
        Assert.Null(updatedBed.AssignedCreatureId);
    }
}
