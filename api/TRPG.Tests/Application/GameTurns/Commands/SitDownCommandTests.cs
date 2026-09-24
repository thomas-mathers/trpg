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
        _player = Builders.MakeCreature();
        _seat = Builders.MakeSeat(_player.WorldId, _player.LocationId);
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
        Assert.Equal(CreatureState.Sitting, player.State);
        Assert.Equal(_player.Id, seat.OccupantId);
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
        Assert.Equal(CreatureState.Idle, player.State);
    }

    [Fact]
    public async Task StandUp_ClearsSeatAndSetsPlayerIdle_WhenPlayerIsSitting()
    {
        _player.State = CreatureState.Sitting;
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
        Assert.Equal(CreatureState.Idle, player.State);
        Assert.Null(seat.OccupantId);
    }
}
