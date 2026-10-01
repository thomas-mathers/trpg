using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

public sealed class StandUpCommandTests(DatabaseFixture db)
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
        _player = Builders.MakeCreature(
            posture: CreaturePosture.Sitting,
            x: 4.5,
            y: 7.25,
            angle: 1.5
        );
        _player.StandingX = 2;
        _player.StandingY = 3;
        _player.StandingAngle = 0.5;
        _seat = Builders.MakeSeat(
            _player.WorldId,
            _player.LocationId,
            _player.Id,
            x: 4.5,
            y: 7.25,
            angle: 1.5
        );
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
    public async Task Handle_RestoresThePreSitPose_WhenThePlayerStandsUp()
    {
        var handler = _services.GetRequiredService<StandUpCommandHandler>();

        await handler.Handle(
            new StandUpCommand { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal((2d, 3d, 0.5), (player.X, player.Y, player.Angle));
        Assert.Null(player.StandingX);
        Assert.Null(player.StandingY);
        Assert.Null(player.StandingAngle);
    }

    [Fact]
    public async Task Handle_VacatesTheSeatAndStandsThePlayer_WhenThePlayerStandsUp()
    {
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
