using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

public sealed class SitDownCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private Creature _player = null!;
    private Seat _seat = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _player = Builders.MakeCreature(x: 1, y: 2, angle: 0.25);
        _seat = Builders.MakeSeat(_player.WorldId, _player.LocationId, x: 4.5, y: 7.25, angle: 1.5);
        _context.Creatures.Add(_player);
        _context.Props.Add(_seat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_OccupiesSeatAndSetsPlayerSitting_WhenSeatIsAvailable()
    {
        var handler = _services.GetRequiredService<SitDownCommandHandler>();

        var result = await handler.Handle(
            new SitDownCommand { PlayerId = _player.Id, SeatId = _seat.Id },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        var seat = await verifyContext
            .Props.OfType<Seat>()
            .SingleAsync(prop => prop.Id == _seat.Id, TestContext.Current.CancellationToken);
        Assert.Equal(SitDownResult.Success, result);
        Assert.Equal(CreaturePosture.Sitting, player.Posture);
        Assert.Equal(_player.Id, seat.OccupantId);
    }

    [Fact]
    public async Task Handle_AlignsPlayerPoseToSeat_WhenSeatIsAvailable()
    {
        var handler = _services.GetRequiredService<SitDownCommandHandler>();

        await handler.Handle(
            new SitDownCommand { PlayerId = _player.Id, SeatId = _seat.Id },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(4.5, player.X);
        Assert.Equal(7.25, player.Y);
        Assert.Equal(1.5, player.Angle);
    }

    [Fact]
    public async Task Handle_RemembersThePreSitPose_WhenSeatIsAvailable()
    {
        var handler = _services.GetRequiredService<SitDownCommandHandler>();

        await handler.Handle(
            new SitDownCommand { PlayerId = _player.Id, SeatId = _seat.Id },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal((1d, 2d, 0.25), (player.StandingX, player.StandingY, player.StandingAngle));
    }

    [Fact]
    public async Task Handle_LeavesPlayerIdle_WhenSeatIsOccupied()
    {
        _seat.OccupantId = Guid.NewGuid();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = _services.GetRequiredService<SitDownCommandHandler>();

        var result = await handler.Handle(
            new SitDownCommand { PlayerId = _player.Id, SeatId = _seat.Id },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(SitDownResult.SeatOccupied, result);
        Assert.Null(player.Activity);
    }

    [Fact]
    public async Task StandUp_ClearsSeatAndSetsPlayerIdle_WhenPlayerIsSitting()
    {
        _player.Posture = CreaturePosture.Sitting;
        _seat.OccupantId = _player.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = _services.GetRequiredService<StandUpCommandHandler>();

        var result = await handler.Handle(
            new StandUpCommand { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        var seat = await verifyContext
            .Props.OfType<Seat>()
            .SingleAsync(prop => prop.Id == _seat.Id, TestContext.Current.CancellationToken);
        Assert.Equal(StandUpResult.Success, result);
        Assert.Equal(CreaturePosture.Standing, player.Posture);
        Assert.Null(seat.OccupantId);
    }
}
