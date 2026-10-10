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
}
