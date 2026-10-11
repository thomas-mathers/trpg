using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class PlanCurrentLocalActivityCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PlansLocalWork_WhenTheDueJobIsAlreadyInTheCurrentLocation()
    {
        var world = Builders.MakeWorld();
        var state = Builders.MakeState(Guid.NewGuid(), worldId: world.Id);
        var room = Builders.MakeLocation(
            world.Id,
            state.Id,
            roomId: Guid.NewGuid(),
            width: 12,
            depth: 12
        );
        var creature = Builders.MakeCreature(world.Id, locationId: room.Id);
        var workstation = Builders.MakeWorkstation(world.Id, room.Id);
        workstation.X = 7;
        workstation.Y = 7;
        var job = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Work,
            startHour: 8,
            endHour: 17,
            locationId: room.Id,
            worldId: world.Id
        );
        _context.Worlds.Add(world);
        _context.States.Add(state);
        _context.Locations.Add(room);
        _context.Creatures.Add(creature);
        _context.CreatureJobs.Add(job);
        _context.Props.Add(workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = _services.GetRequiredService<
            ICommandHandler<PlanCurrentLocalActivityCommand, LocalActivityPlanResult>
        >();

        var result = await handler.Handle(
            new PlanCurrentLocalActivityCommand
            {
                CreatureId = creature.Id,
                At = new GameInstant(new DateTime(2000, 1, 3, 10, 0, 0)),
            },
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Handled);
        Assert.NotNull(result.Move);
        Assert.Equal(workstation.Id, result.Move.TargetPropId);
    }

    [Fact]
    public async Task Handle_LeavesTheJourneyToTheRoutinePlanner_WhenTheIdleJobIsInSevereWeather()
    {
        // Arrange
        var creature = await SeedCreatureWithIdleJob(jobAt: creatureRoom => creatureRoom);
        var command = ShelterCommand(creature, Guid.NewGuid(), creature.LocationId);

        // Act
        var result = await Handler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Handled);
    }

    [Fact]
    public async Task Handle_SettlesLocally_WhenTheCreatureIsAlreadyAtItsShelter()
    {
        // Arrange
        var otherRoomId = Guid.NewGuid();
        var creature = await SeedCreatureWithIdleJob(jobAt: _ => otherRoomId);
        var command = ShelterCommand(creature, creature.LocationId, otherRoomId);

        // Act
        var result = await Handler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Handled);
    }

    private ICommandHandler<PlanCurrentLocalActivityCommand, LocalActivityPlanResult> Handler() =>
        _services.GetRequiredService<
            ICommandHandler<PlanCurrentLocalActivityCommand, LocalActivityPlanResult>
        >();

    private static PlanCurrentLocalActivityCommand ShelterCommand(
        Creature creature,
        Guid shelterLocationId,
        Guid severeLocationId
    ) =>
        new()
        {
            CreatureId = creature.Id,
            At = new GameInstant(new DateTime(2000, 1, 3, 10, 0, 0)),
            ShelterLocationId = shelterLocationId,
            SevereWeatherLocationIds = new HashSet<Guid> { severeLocationId },
        };

    private async Task<Creature> SeedCreatureWithIdleJob(Func<Guid, Guid> jobAt)
    {
        var world = Builders.MakeWorld();
        var state = Builders.MakeState(Guid.NewGuid(), worldId: world.Id);
        var room = Builders.MakeLocation(
            world.Id,
            state.Id,
            roomId: Guid.NewGuid(),
            width: 12,
            depth: 12
        );
        var creature = Builders.MakeCreature(world.Id, locationId: room.Id);
        var jobLocationId = jobAt(room.Id);
        _context.Worlds.Add(world);
        _context.States.Add(state);
        _context.Locations.Add(room);
        if (jobLocationId != room.Id)
        {
            _context.Locations.Add(
                Builders.MakeLocation(world.Id, state.Id, id: jobLocationId, roomId: Guid.NewGuid())
            );
        }
        _context.Creatures.Add(creature);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Idle,
                startHour: 8,
                endHour: 17,
                locationId: jobLocationId,
                worldId: world.Id
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }
}
