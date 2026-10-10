using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.WorldSimulation.Arrivals;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class ExecuteJourneyArrivalsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 13, 0, 0));

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<ExecuteJourneyArrivalsCommand, JourneyArrivalResult> _arrive = null!;
    private ICommandHandler<CompleteLocalMoveCommand, LocalMovePlan?> _complete = null!;
    private Location _room = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _arrive = _serviceProvider.GetRequiredService<
            ICommandHandler<ExecuteJourneyArrivalsCommand, JourneyArrivalResult>
        >();
        _complete = _serviceProvider.GetRequiredService<
            ICommandHandler<CompleteLocalMoveCommand, LocalMovePlan?>
        >();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId);
        _room = Builders.MakeLocation(
            WorldId,
            state.Id,
            roomId: Guid.NewGuid(),
            width: 12,
            depth: 12
        );
        _context.States.Add(state);
        _context.Locations.Add(_room);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PlansALocalWalkWithoutClaimingTheWorkstation()
    {
        var worker = Builders.MakeCreature(WorldId, locationId: _room.Id);
        var counter = Builders.MakeWorkstation(WorldId, _room.Id);
        counter.X = 6;
        counter.Y = 6;
        counter.Width = 2;
        counter.Depth = 1;
        _context.Creatures.Add(worker);
        _context.Props.Add(counter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _arrive.Handle(
            Arrive(worker.Id, CreatureJobAction.Work),
            TestContext.Current.CancellationToken
        );

        var move = Assert.Single(result.LocalMoves);
        Assert.Equal(counter.Id, move.TargetPropId);
        Assert.Equal(LocalMoveTargetKind.Workstation, move.TargetKind);
        Assert.Null(await db.ReadPropOccupantId(counter.Id));
        Assert.True(move.Path.Count >= 2);
    }

    [Fact]
    public async Task Complete_ClaimsTheWorkstationOnlyAfterTheLocalWalkFinishes()
    {
        var worker = Builders.MakeCreature(WorldId, locationId: _room.Id);
        var counter = Builders.MakeWorkstation(WorldId, _room.Id);
        _context.Creatures.Add(worker);
        _context.Props.Add(counter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var move = Assert.Single(
            (
                await _arrive.Handle(
                    Arrive(worker.Id, CreatureJobAction.Work),
                    TestContext.Current.CancellationToken
                )
            ).LocalMoves
        );

        var retry = await _complete.Handle(
            new CompleteLocalMoveCommand { Move = move, StopPosition = move.Path[^1] },
            TestContext.Current.CancellationToken
        );

        Assert.Null(retry);
        Assert.Equal(worker.Id, await db.ReadPropOccupantId(counter.Id));
        Assert.Equal(CreatureActivity.Working, (await db.ReadCreature(worker.Id)).Activity);
    }

    [Fact]
    public async Task Complete_ReplansToAnotherWorkstation_WhenTheTargetWasClaimedDuringTheWalk()
    {
        var worker = Builders.MakeCreature(WorldId, locationId: _room.Id);
        var other = Builders.MakeCreature(WorldId, locationId: _room.Id);
        var near = Builders.MakeWorkstation(WorldId, _room.Id);
        near.X = 2;
        near.Y = 2;
        var far = Builders.MakeWorkstation(WorldId, _room.Id);
        far.X = 9;
        far.Y = 9;
        _context.Creatures.AddRange(worker, other);
        _context.Props.AddRange(near, far);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var move = Assert.Single(
            (
                await _arrive.Handle(
                    Arrive(worker.Id, CreatureJobAction.Work),
                    TestContext.Current.CancellationToken
                )
            ).LocalMoves
        );
        await _context
            .Props.OfType<Workstation>()
            .Where(workstation => workstation.Id == move.TargetPropId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(workstation => workstation.OccupantId, other.Id),
                TestContext.Current.CancellationToken
            );

        var retry = await _complete.Handle(
            new CompleteLocalMoveCommand { Move = move, StopPosition = move.Path[^1] },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(retry);
        Assert.Equal(far.Id, retry.TargetPropId);
        Assert.Equal(LocalMoveTargetKind.Workstation, retry.TargetKind);
    }

    [Fact]
    public async Task Handle_PlansAChairWalk_WhenNoWorkstationIsAvailable()
    {
        var worker = Builders.MakeCreature(WorldId, locationId: _room.Id);
        var chair = Builders.MakeSeat(WorldId, _room.Id, x: 5, y: 5);
        _context.Creatures.Add(worker);
        _context.Props.Add(chair);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var move = Assert.Single(
            (
                await _arrive.Handle(
                    Arrive(worker.Id, CreatureJobAction.Work),
                    TestContext.Current.CancellationToken
                )
            ).LocalMoves
        );

        Assert.Equal(chair.Id, move.TargetPropId);
        Assert.Equal(LocalMoveTargetKind.Seat, move.TargetKind);
    }

    [Fact]
    public async Task Handle_CompletesWorkStanding_WhenNoPropIsAvailable()
    {
        var worker = Builders.MakeCreature(WorldId, locationId: _room.Id);
        _context.Creatures.Add(worker);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _arrive.Handle(
            Arrive(worker.Id, CreatureJobAction.Work),
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result.LocalMoves);
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(CreatureActivity.Working, updated.Activity);
        Assert.Equal(CreaturePosture.Standing, updated.Posture);
    }

    private ExecuteJourneyArrivalsCommand Arrive(Guid creatureId, CreatureJobAction action) =>
        new()
        {
            WorldId = WorldId,
            Arrivals =
            [
                new JourneyCompleted(creatureId, Now, _room.Id, Guid.NewGuid(), action)
                {
                    StopPosition = new Point(1, 1),
                },
            ],
        };
}
