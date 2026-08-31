using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Commands;

[Collection("Database")]
public sealed class CreateRoomBookingCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CreateRoomBookingCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<CreateRoomBookingCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesTheRoomBooking()
    {
        // Arrange
        var booking = new RoomBooking
        {
            WorldId = WorldId,
            RoomId = Guid.NewGuid(),
            KeyItemId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            DueAtPlaytime = TimeSpan.FromHours(24),
        };

        // Act
        await _handler.Handle(
            new CreateRoomBookingCommand { RoomBooking = booking },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.RoomBookings.SingleAsync(
            b => b.Id == booking.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(booking.RoomId, persisted.RoomId);
        Assert.Equal(booking.KeyItemId, persisted.KeyItemId);
        Assert.Equal(booking.PlayerId, persisted.PlayerId);
        Assert.Equal(booking.DueAtPlaytime, persisted.DueAtPlaytime);
    }
}
