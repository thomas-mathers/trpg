using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

public sealed class TryOccupyAnyAvailableSeatCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private TryOccupyAnyAvailableSeatCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<TryOccupyAnyAvailableSeatCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ClaimsTheSeat_WhenOneIsAvailable()
    {
        // Arrange
        var seat = Builders.MakeSeat(WorldId, LocationId);
        _context.Props.Add(seat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var creatureId = Guid.NewGuid();

        // Act
        var claimed = await _handler.Handle(
            new TryOccupyAnyAvailableSeatCommand
            {
                LocationId = LocationId,
                CreatureId = creatureId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(claimed);
        var updatedSeat = await _context
            .Props.OfType<Seat>()
            .AsNoTracking()
            .SingleAsync(s => s.Id == seat.Id, TestContext.Current.CancellationToken);
        Assert.Equal(creatureId, updatedSeat.OccupantId);
    }

    [Fact]
    public async Task Handle_SkipsAnOccupiedSeat_AndClaimsTheNextOne()
    {
        // Arrange — guards the retry-with-exclusion loop against getting stuck on the first candidate
        var occupiedSeat = Builders.MakeSeat(WorldId, LocationId, occupantId: Guid.NewGuid());
        var freeSeat = Builders.MakeSeat(WorldId, LocationId);
        _context.Props.AddRange(occupiedSeat, freeSeat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var creatureId = Guid.NewGuid();

        // Act
        var claimed = await _handler.Handle(
            new TryOccupyAnyAvailableSeatCommand
            {
                LocationId = LocationId,
                CreatureId = creatureId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(claimed);
        var updatedFreeSeat = await _context
            .Props.OfType<Seat>()
            .AsNoTracking()
            .SingleAsync(s => s.Id == freeSeat.Id, TestContext.Current.CancellationToken);
        Assert.Equal(creatureId, updatedFreeSeat.OccupantId);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoSeatIsAvailable()
    {
        // Arrange
        var occupiedSeat = Builders.MakeSeat(WorldId, LocationId, occupantId: Guid.NewGuid());
        _context.Props.Add(occupiedSeat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var claimed = await _handler.Handle(
            new TryOccupyAnyAvailableSeatCommand
            {
                LocationId = LocationId,
                CreatureId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(claimed);
    }

    [Fact]
    public async Task Handle_ReturnsTheSeatPose_WhenTheSeatIsClaimed()
    {
        // Arrange
        var seat = Builders.MakeSeat(WorldId, LocationId, x: 2.5, y: 3.5, angle: 0.75);
        _context.Props.Add(seat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var claimed = await _handler.Handle(
            new TryOccupyAnyAvailableSeatCommand
            {
                LocationId = LocationId,
                CreatureId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new Placement(X: 2.5, Y: 3.5, Angle: 0.75), claimed?.Placement);
    }
}
