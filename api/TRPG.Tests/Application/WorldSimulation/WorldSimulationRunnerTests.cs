using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Scenes;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class WorldSimulationRunnerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 13, 0, 0));

    private readonly Guid _worldId = Guid.NewGuid();
    private Guid _playerId;

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WorldSimulationRunner _runner = null!;
    private Location _home = null!;
    private Location _workplace = null!;
    private LocationConnector _door = null!;

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
        _door = Builders.MakeLocationConnector(_home.Id, _workplace.Id, worldId: _worldId);
        _context.States.Add(state);
        _context.Locations.AddRange(_home, _workplace);
        _context.LocationConnectors.Add(_door);
        _context.TravelNodes.AddRange(
            Builders.MakeExitNode(_door, 4, 5),
            Builders.MakeArrivalNode(_door, 6, 7)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Tick_MovesAWorkerToTheirJobAndStartsTheJob()
    {
        // Arrange
        var worker = await AddWorker();
        await StartRunner();

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(
            (_workplace.Id, CreatureActivity.Working),
            (updated.LocationId, updated.Activity)
        );
    }

    [Fact]
    public async Task Tick_LeavesAnEngagedWorkerAlone()
    {
        // Arrange
        var worker = await AddWorker();
        await StartRunner();
        _runner.Post(new EngageCreature(worker.Id));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_home.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_SendsAReleasedWorkerOnTheirWay()
    {
        // Arrange
        var worker = await AddWorker();
        await StartRunner();
        _runner.Post(new EngageCreature(worker.Id));
        await _runner.Tick(Now, TestContext.Current.CancellationToken);
        _runner.Post(new ReleaseCreature(worker.Id));

        // Act
        await _runner.Tick(Now + TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_LeavesARemovedWorkerAlone()
    {
        // Arrange
        var worker = await AddWorker();
        await StartRunner();
        _runner.Post(new RemoveCreature(worker.Id));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_home.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_SimulatesASpawnedWorker()
    {
        // Arrange
        await StartRunner();
        var worker = await AddWorker();
        _runner.Post(
            new SpawnCreature(
                new SimCreatureSeed(worker.Id, _home.Id, 50, await ReadJobs(worker.Id))
            )
        );

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_SimulatesACreature_WhenItIsTracked()
    {
        // Arrange
        var captive = await AddWorker(isRestrained: true);
        await StartRunner();
        captive.IsRestrained = false;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _runner.Post(new TrackCreature(captive.Id));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(captive.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_PublishesTheScene_WhenAWorkerArrivesAtThePlayersLocation()
    {
        // Arrange
        await AddWorker();
        await AddPlayerAt(_workplace);
        await StartRunner();

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(
            _serviceProvider.GetRequiredService<PublishedSceneRegistry>().Find(_playerId)
        );
    }

    [Fact]
    public async Task Tick_StandsAPatrollingGuardAtItsFirstStop()
    {
        // Arrange
        var route = await _context.AddPatrolRoute(
            _worldId,
            _door,
            TestContext.Current.CancellationToken
        );
        var guard = await AddGuard(route);
        await StartRunner();

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(guard.Id);
        Assert.Equal(
            (_workplace.Id, CreatureMovement.Stationary),
            (updated.LocationId, updated.Movement)
        );
    }

    private async Task AddPlayerAt(Location location)
    {
        var player = Builders.MakeCreature(_worldId, locationId: location.Id);
        _playerId = player.Id;
        var world = Builders.MakeWorld(_worldId);
        world.PlayerId = player.Id;
        _context.Worlds.Add(world);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task StartRunner()
    {
        var factory = _serviceProvider.GetRequiredService<WorldSimulationRunnerFactory>();
        _runner = await factory.Create(_worldId, Now, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<CreatureJob>> ReadJobs(Guid creatureId)
    {
        await using var readContext = db.CreateContext();
        return await readContext
            .CreatureJobs.Where(job => job.CreatureId == creatureId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Creature> AddGuard(Route route)
    {
        var guard = Builders.MakeCreature(_worldId, locationId: _workplace.Id, movementSpeed: 50);
        _context.Creatures.Add(guard);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                guard.Id,
                action: CreatureJobAction.Work,
                startHour: 0,
                endHour: 24,
                locationId: _workplace.Id,
                worldId: _worldId,
                routeId: route.Id
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return guard;
    }

    private async Task<Creature> AddWorker(bool isRestrained = false)
    {
        var worker = Builders.MakeCreature(
            _worldId,
            locationId: _home.Id,
            movementSpeed: 50,
            isRestrained: isRestrained
        );
        _context.Creatures.Add(worker);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                worker.Id,
                action: CreatureJobAction.Work,
                startHour: 0,
                endHour: 24,
                locationId: _workplace.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return worker;
    }
}
