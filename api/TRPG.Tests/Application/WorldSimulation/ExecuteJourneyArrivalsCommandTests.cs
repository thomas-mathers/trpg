using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.WorldSimulation.Arrivals;
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
    private ICommandHandler<ExecuteJourneyArrivalsCommand, JourneyArrivalResult> _handler = null!;
    private Location _room = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<ExecuteJourneyArrivalsCommand, JourneyArrivalResult>
        >();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId);
        _room = Builders.MakeLocation(WorldId, state.Id, roomId: Guid.NewGuid());
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
    public async Task Handle_PutsTheCreatureToSleepInItsBed_WhenTheJobIsSleep()
    {
        // Arrange
        var creature = Builders.MakeCreature(WorldId, locationId: _room.Id);
        var bed = Builders.MakeBed(WorldId, _room.Id, assignedCreatureId: creature.Id);
        _context.Creatures.Add(creature);
        _context.Props.Add(bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            Arrive(creature.Id, CreatureJobAction.Sleep, bed.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Sleeping, updated.Condition);
        Assert.Equal(creature.Id, await db.ReadPropOccupantId(bed.Id));
    }

    [Fact]
    public async Task Handle_StaffsTheCounter_WhenAWorkerArrivesAtAWorkRoom()
    {
        // Arrange
        var worker = Builders.MakeCreature(
            WorldId,
            locationId: _room.Id,
            activity: CreatureActivity.Working
        );
        var counter = Builders.MakeWorkstation(WorldId, _room.Id, assignedCreatureId: worker.Id);
        _context.Creatures.Add(worker);
        _context.Props.Add(counter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            Arrive(worker.Id, CreatureJobAction.Work, counter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(worker.Id, await db.ReadPropOccupantId(counter.Id));
    }

    [Fact]
    public async Task Handle_ClaimsOnlyTheSeatReachedByTheJourney()
    {
        // Arrange
        var creature = Builders.MakeCreature(WorldId, locationId: _room.Id);
        var otherSeat = new Seat
        {
            Id = new Guid("00000000-0000-0000-0000-000000000001"),
            WorldId = WorldId,
            LocationId = _room.Id,
            Name = "Other chair",
            Description = "A test chair",
        };
        var reachedSeat = new Seat
        {
            Id = new Guid("00000000-0000-0000-0000-000000000002"),
            WorldId = WorldId,
            LocationId = _room.Id,
            Name = "Reached chair",
            Description = "A test chair",
        };
        _context.Creatures.Add(creature);
        _context.Props.AddRange(otherSeat, reachedSeat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ExecuteJourneyArrivalsCommand
            {
                WorldId = WorldId,
                Arrivals =
                [
                    new JourneyCompleted(
                        creature.Id,
                        Now,
                        _room.Id,
                        Guid.NewGuid(),
                        CreatureJobAction.Idle,
                        DestinationPropId: reachedSeat.Id
                    ),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(await db.ReadPropOccupantId(otherSeat.Id));
        Assert.Equal(creature.Id, await db.ReadPropOccupantId(reachedSeat.Id));
    }

    [Fact]
    public async Task Handle_LeavesTheCreatureAlone_WhenItIsNotAtTheJobLocation()
    {
        // Arrange
        var creature = Builders.MakeCreature(WorldId);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            Arrive(creature.Id, CreatureJobAction.Sleep),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Awake, updated.Condition);
    }

    [Fact]
    public async Task Handle_CompletesStanding_WhenAnIdleJourneyHasNoSeatTarget()
    {
        // Arrange
        var creature = Builders.MakeCreature(
            WorldId,
            locationId: _room.Id,
            activity: CreatureActivity.Working
        );
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            Arrive(creature.Id, CreatureJobAction.Idle),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Awake, updated.Condition);
        Assert.Null(updated.Activity);
        Assert.Equal(CreaturePosture.Standing, updated.Posture);
    }

    private ExecuteJourneyArrivalsCommand Arrive(
        Guid creatureId,
        CreatureJobAction action,
        Guid? destinationPropId = null
    ) =>
        new()
        {
            WorldId = WorldId,
            Arrivals =
            [
                new JourneyCompleted(
                    creatureId,
                    Now,
                    _room.Id,
                    Guid.NewGuid(),
                    action,
                    DestinationPropId: destinationPropId
                ),
            ],
        };
}
