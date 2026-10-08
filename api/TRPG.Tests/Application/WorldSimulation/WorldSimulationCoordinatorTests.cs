using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.EventHandlers;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class WorldSimulationCoordinatorTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 13, 0, 0));

    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private Location _home = null!;
    private Location _workplace = null!;
    private Creature _worker = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .WithScopedDbContexts(db.ConnectionString)
            .AddScoped<IGameClientEventDispatcher, NoOpGameClientEventDispatcher>()
            .BuildServiceProvider();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: _worldId);
        _home = Builders.MakeLocation(_worldId, state.Id);
        _workplace = Builders.MakeLocation(_worldId, state.Id);
        var door = Builders.MakeLocationConnector(_home.Id, _workplace.Id, worldId: _worldId);
        _worker = Builders.MakeCreature(_worldId, locationId: _home.Id, movementSpeed: 50);
        _context.States.Add(state);
        _context.Locations.AddRange(_home, _workplace);
        _context.LocationConnectors.Add(door);
        _context.TravelNodes.AddRange(
            Builders.MakeExitNode(door, 4, 5),
            Builders.MakeArrivalNode(door, 6, 7)
        );
        _context.Creatures.Add(_worker);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                _worker.Id,
                action: CreatureJobAction.Work,
                startHour: 0,
                endHour: 24,
                locationId: _workplace.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Tick_MovesWorkersInActiveWorlds()
    {
        // Arrange
        var coordinator = MakeCoordinator(activeWorldIds: [_worldId]);

        // Act
        await coordinator.Tick(TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(_worker.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_LeavesInactiveWorldsAlone()
    {
        // Arrange
        var coordinator = MakeCoordinator(activeWorldIds: []);

        // Act
        await coordinator.Tick(TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(_worker.Id);
        Assert.Equal(_home.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_MovesCreaturesSpawnedAfterTheSimulationStarted()
    {
        // Arrange
        var clock = new FixedWorldClock([_worldId], Now);
        var coordinator = MakeCoordinator([_worldId], clock);
        await coordinator.Tick(TestContext.Current.CancellationToken);
        var spawned = Builders.MakeCreature(_worldId, locationId: _home.Id, movementSpeed: 50);
        _context.Creatures.Add(spawned);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                spawned.Id,
                action: CreatureJobAction.Work,
                startHour: 20,
                endHour: 22,
                locationId: _workplace.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = new CreaturesSpawnedSimulationEventHandler(coordinator);
        await handler.Handle(
            new CreaturesSpawnedEvent(_worldId, [spawned.Id]),
            TestContext.Current.CancellationToken
        );
        clock.Now = new GameInstant(new DateTime(2000, 1, 3, 21, 0, 0));

        // Act
        await coordinator.Tick(TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(spawned.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_LeavesDeadCreaturesWhereTheyDied()
    {
        // Arrange
        var sleeper = Builders.MakeCreature(_worldId, locationId: _home.Id, movementSpeed: 50);
        _context.Creatures.Add(sleeper);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                sleeper.Id,
                action: CreatureJobAction.Work,
                startHour: 20,
                endHour: 22,
                locationId: _workplace.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var clock = new FixedWorldClock([_worldId], Now);
        var coordinator = MakeCoordinator([_worldId], clock);
        await coordinator.Tick(TestContext.Current.CancellationToken);
        var handler = new CreaturesDiedSimulationEventHandler(coordinator);
        await handler.Handle(
            new CreaturesDiedEvent(_worldId, [sleeper.Id]),
            TestContext.Current.CancellationToken
        );
        clock.Now = new GameInstant(new DateTime(2000, 1, 3, 21, 0, 0));

        // Act
        await coordinator.Tick(TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(sleeper.Id);
        Assert.Equal(_home.Id, updated.LocationId);
    }

    private WorldSimulationCoordinator MakeCoordinator(
        IReadOnlyCollection<Guid> activeWorldIds,
        FixedWorldClock? clock = null
    )
    {
        clock ??= new FixedWorldClock(activeWorldIds, Now);
        var factory = new WorldSimulationRunnerFactory(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<IWorldMutationGate>()
        );

        return new WorldSimulationCoordinator(
            clock,
            factory,
            NullLogger<WorldSimulationCoordinator>.Instance
        );
    }

    private sealed class FixedWorldClock(IReadOnlyCollection<Guid> activeWorldIds, GameInstant now)
        : IWorldClock
    {
        public GameInstant Now { get; set; } = now;

        public IReadOnlyCollection<Guid> GetActiveWorldIds() => activeWorldIds;

        public Task<GameInstant> GetCurrent(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Now);

        public Task<GameInstant> ResumeWorld(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<GameInstant> PauseWorld(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<GameInstant> Advance(
            Guid worldId,
            TimeSpan duration,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<GameInstant> Checkpoint(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task CheckpointActiveWorlds(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
