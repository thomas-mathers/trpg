using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SyncActiveLocationRoutinesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncActiveLocationRoutinesCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncActiveLocationRoutinesCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SyncsEveryDistinctLocation_WhenPlayersShareOneOfThem()
    {
        // Arrange
        var firstLocation = Builders.MakeLocation(WorldId, kind: LocationKind.Wilderness);
        var secondLocation = Builders.MakeLocation(WorldId, kind: LocationKind.Wilderness);
        var firstSleeper = Builders.MakeCreature(WorldId, locationId: firstLocation.Id);
        var secondSleeper = Builders.MakeCreature(WorldId, locationId: secondLocation.Id);
        _context.Locations.AddRange(firstLocation, secondLocation);
        _context.Creatures.AddRange(firstSleeper, secondSleeper);
        _context.CreatureJobs.AddRange(
            MakeSleepJob(firstSleeper.Id, firstLocation.Id),
            MakeSleepJob(secondSleeper.Id, secondLocation.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SyncActiveLocationRoutinesCommand
            {
                WorldId = WorldId,
                GameTime = GameClock.Epoch,
                Players =
                [
                    new ActiveLocationPlayer(firstLocation.Id, Guid.NewGuid(), PlayerLevel: 1),
                    new ActiveLocationPlayer(firstLocation.Id, Guid.NewGuid(), PlayerLevel: 4),
                    new ActiveLocationPlayer(secondLocation.Id, Guid.NewGuid(), PlayerLevel: 2),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var states = await verifyContext
            .Creatures.Where(creature =>
                creature.Id == firstSleeper.Id || creature.Id == secondSleeper.Id
            )
            .Select(creature => creature.State)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, states.Length);
        Assert.All(states, state => Assert.Equal(CreatureState.Sleeping, state));
    }

    private static CreatureJob MakeSleepJob(Guid creatureId, Guid locationId) =>
        Builders.MakeCreatureJob(
            creatureId,
            action: CreatureJobAction.Sleep,
            startHour: 6,
            endHour: 22,
            locationId: locationId,
            worldId: WorldId,
            priority: 100
        );
}
