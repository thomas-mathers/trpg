using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Creatures;
using TRPG.Application.Scenes;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;
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
    private static readonly TimeSpan ActivePassInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromSeconds(30);

    private readonly Guid _worldId = Guid.NewGuid();
    private Guid _stateId;
    private Guid _playerId;
    private readonly SwitchableOptions _options = new(
        new WorldSimulationOptions { RouteSearchesPerTick = 1 }
    );

    private readonly RecordingDispatcher _dispatcher = new();
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
            .AddSingleton<IOptions<WorldSimulationOptions>>(_options)
            .WithScopedDbContexts(db.ConnectionString)
            .AddScoped<IGameClientEventDispatcher>(_ => _dispatcher)
            .BuildServiceProvider();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: _worldId);
        _stateId = state.Id;
        _context.Worlds.Add(Builders.MakeWorld(_worldId, Now));
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
    public async Task Tick_PlansOnlyTheConfiguredNumberOfRoutes()
    {
        // Arrange
        var first = await AddWorker();
        var second = await AddWorker();
        await StartRunner();

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var creatures = new[] { await db.ReadCreature(first.Id), await db.ReadCreature(second.Id) };
        Assert.Single(creatures, creature => creature.LocationId == _workplace.Id);
        Assert.Single(creatures, creature => creature.LocationId == _home.Id);
    }

    [Fact]
    public async Task Tick_RotatesRoutePlanningCandidates()
    {
        // Arrange
        await AddResident();
        var worker = await AddWorker();
        await StartRunner();

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);
        await _runner.Tick(Now + TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
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
    public async Task Tick_PersistsThePlayersWalkedPosition_WhenPlayerInputWasApplied()
    {
        // Arrange
        _home.Width = 100;
        _home.Depth = 100;
        await AddPlayerAt(_home, x: 50, y: 50);
        await StartRunner();
        var receivedAt = DateTimeOffset.UtcNow;
        _runner.Post(
            new PlayerInput(
                _playerId,
                _home.Id,
                new MovementInput(1, 0, 0),
                new Point(50, 50),
                receivedAt
            )
        );
        _runner.Post(
            new PlayerInput(
                _playerId,
                _home.Id,
                new MovementInput(0, 0, 0),
                new Point(50, 48),
                receivedAt.AddSeconds(1)
            )
        );

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var player = await db.ReadCreature(_playerId);
        Assert.InRange(player.Y, 48, 49);
    }

    [Fact]
    public async Task Tick_PersistsTheLivePose_WhenThePlayerDisconnected()
    {
        // Arrange
        _home.Width = 100;
        _home.Depth = 100;
        await AddPlayerAt(_home, x: 50, y: 50);
        await StartRunner();
        _runner.Post(
            new PlayerInput(
                _playerId,
                _home.Id,
                new MovementInput(0, 0, 1.5),
                new Point(50, 50),
                DateTimeOffset.UtcNow
            )
        );
        _runner.Post(new PlayerDisconnected(_playerId));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var player = await db.ReadCreature(_playerId);
        Assert.Equal((50d, 50d, 1.5d), (player.X, player.Y, player.Angle));
    }

    [Fact]
    public async Task Tick_StartsFromTheStoredPosition_WhenThePoseWasForgotten()
    {
        // Arrange
        _home.Width = 100;
        _home.Depth = 100;
        await AddPlayerAt(_home, x: 50, y: 50);
        await StartRunner();
        var receivedAt = DateTimeOffset.UtcNow;
        _runner.Post(
            new PlayerInput(
                _playerId,
                _home.Id,
                new MovementInput(1, 0, 0),
                new Point(50, 50),
                receivedAt
            )
        );
        _runner.Post(new ForgetPlayerPose(_playerId));
        _runner.Post(
            new PlayerInput(
                _playerId,
                _home.Id,
                new MovementInput(0, 0, 0),
                new Point(50, 50),
                receivedAt.AddSeconds(1)
            )
        );

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var player = await db.ReadCreature(_playerId);
        Assert.Equal(50d, player.Y);
    }

    [Fact]
    public async Task Tick_HandsARequestTheLivePose_WhenInputPrecedesIt()
    {
        // Arrange
        _home.Width = 100;
        _home.Depth = 100;
        await AddPlayerAt(_home, x: 50, y: 50);
        await StartRunner();
        _runner.Post(
            new PlayerInput(
                _playerId,
                _home.Id,
                new MovementInput(0, 0, 1.5),
                new Point(50, 50),
                DateTimeOffset.UtcNow
            )
        );
        PlayerPose? seen = null;
        _runner.Post(
            new SimulationRequest(
                (poses, _) =>
                {
                    seen = poses.Find(_playerId);
                    poses.Forget(_playerId);
                    return Task.CompletedTask;
                }
            )
        );

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1.5, seen?.Input.Heading);
    }

    [Fact]
    public async Task Tick_DoesNotSaveTheLivePose_WhenARequestForgotIt()
    {
        // Arrange
        _home.Width = 100;
        _home.Depth = 100;
        await AddPlayerAt(_home, x: 50, y: 50);
        await StartRunner();
        _runner.Post(
            new PlayerInput(
                _playerId,
                _home.Id,
                new MovementInput(0, 0, 1.5),
                new Point(50, 50),
                DateTimeOffset.UtcNow
            )
        );
        _runner.Post(
            new SimulationRequest(
                (poses, _) =>
                {
                    poses.Forget(_playerId);
                    return Task.CompletedTask;
                }
            )
        );

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var player = await db.ReadCreature(_playerId);
        Assert.Equal(0d, player.Angle);
    }

    [Fact]
    public async Task Tick_LeavesTheRestOfTheMailboxQueued_WhenTheMessageCapIsReached()
    {
        // Arrange
        var worker = await AddWorker();
        _options.Current = new WorldSimulationOptions
        {
            RouteSearchesPerTick = 1,
            MessagesPerTick = 1,
        };
        await StartRunner();
        _runner.Post(new EngageCreature(worker.Id));
        _runner.Post(new ReleaseCreature(worker.Id));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_home.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_LeavesTheRestOfTheMailboxQueued_WhenTheTimeBudgetIsSpent()
    {
        // Arrange
        var worker = await AddWorker();
        _options.Current = new WorldSimulationOptions
        {
            RouteSearchesPerTick = 1,
            MessageTimeBudget = TimeSpan.Zero,
        };
        await StartRunner();
        _runner.Post(new EngageCreature(worker.Id));
        _runner.Post(new ReleaseCreature(worker.Id));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_home.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_ReloadsFromTheDatabase_WhenAMessageFails()
    {
        // Arrange
        var worker = await AddWorker();
        await StartRunner();
        _runner.Post(new EngageCreature(worker.Id));
        _runner.Post(new SimulationRequest(FailingWork));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_RejectsQueuedRequests_WhenTheReloadFails()
    {
        // Arrange
        await StartRunner();
        _options.IsBroken = true;
        Exception? rejection = null;
        _runner.Post(new SimulationRequest(FailingWork));
        _runner.Post(
            new SimulationRequest((_, _) => Task.CompletedTask, error => rejection = error)
        );

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<WorldUnavailableException>(rejection);
    }

    [Fact]
    public async Task Post_Throws_WhenTheReloadFailed()
    {
        // Arrange
        await StartRunner();
        _options.IsBroken = true;
        _runner.Post(new SimulationRequest(FailingWork));
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.Throws<WorldUnavailableException>(() =>
            _runner.Post(new ForgetPlayerPose(Guid.NewGuid()))
        );
    }

    [Fact]
    public async Task Tick_Recovers_WhenTheReloadSucceedsLater()
    {
        // Arrange
        var worker = await AddWorker();
        await StartRunner();
        _options.IsBroken = true;
        _runner.Post(new SimulationRequest(FailingWork));
        await _runner.Tick(Now, TestContext.Current.CancellationToken);
        _options.IsBroken = false;

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_PlansAtTheInstantAfterARequest_WhenTheRequestAdvancedTheClock()
    {
        // Arrange
        var worker = Builders.MakeCreature(_worldId, locationId: _home.Id, movementSpeed: 50);
        _context.Creatures.Add(worker);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                worker.Id,
                action: CreatureJobAction.Work,
                startHour: 20,
                endHour: 22,
                locationId: _workplace.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await StartRunner();
        var clock = _serviceProvider.GetRequiredService<IWorldClock>();
        _runner.Post(
            new SimulationRequest(
                async (_, cancellationToken) =>
                    await clock.Advance(_worldId, TimeSpan.FromHours(8), cancellationToken)
            )
        );

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var updated = await db.ReadCreature(worker.Id);
        Assert.Equal(_workplace.Id, updated.LocationId);
    }

    [Fact]
    public async Task Tick_DoesNotRegenerateThePlayer_OnTheFirstTick()
    {
        // Arrange
        await AddPlayerAt(_home, currentHp: 1);
        await StartRunner();

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        var player = await db.ReadCreature(_playerId);
        Assert.Equal(1, player.CurrentHp);
    }

    [Fact]
    public async Task Tick_DoesNotRegenerateThePlayer_BeforeTheIntervalElapsed()
    {
        // Arrange
        await AddPlayerAt(_home, currentHp: 1);
        await StartRunner();
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Act
        await _runner.Tick(
            Now + ActivePassInterval - TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );

        // Assert
        var player = await db.ReadCreature(_playerId);
        Assert.Equal(1, player.CurrentHp);
    }

    [Fact]
    public async Task Tick_RegeneratesThePlayer_WhenTheIntervalElapsed()
    {
        // Arrange
        await AddPlayerAt(_home, currentHp: 1);
        await StartRunner();
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Act
        await _runner.Tick(Now + ActivePassInterval, TestContext.Current.CancellationToken);

        // Assert
        var player = await db.ReadCreature(_playerId);
        Assert.True(player.CurrentHp > 1);
    }

    [Fact]
    public async Task Tick_FlushesTheWorldEvents_WhenTheActivePassesRan()
    {
        // Arrange
        await AddPlayerAt(_home, currentHp: 1);
        await StartRunner();
        await _runner.Tick(Now, TestContext.Current.CancellationToken);
        var flushesBefore = _dispatcher.FlushedWorldIds.Count(id => id == _worldId);

        // Act
        await _runner.Tick(Now + ActivePassInterval, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(_dispatcher.FlushedWorldIds.Count(id => id == _worldId) > flushesBefore);
    }

    [Fact]
    public async Task Tick_DoesNotRunLocationMaintenance_BeforeTheIntervalElapsed()
    {
        // Arrange
        await AddPlayerAt(_home);
        await StartRunner();
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Act
        await _runner.Tick(
            Now + MaintenanceInterval - TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(await WeatherExists());
    }

    [Fact]
    public async Task Tick_RunsLocationMaintenance_WhenTheIntervalElapsed()
    {
        // Arrange
        await AddPlayerAt(_home);
        await StartRunner();
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Act
        await _runner.Tick(Now + MaintenanceInterval, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(await WeatherExists());
    }

    [Fact]
    public async Task Tick_RunsMaintenanceAndRegenerationAtTheResultingInstant_WhenTheClockSkipsHours()
    {
        // Arrange
        await AddPlayerAt(_home, currentHp: 1);
        await StartRunner();
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Act
        await _runner.Tick(Now + TimeSpan.FromHours(3), TestContext.Current.CancellationToken);

        // Assert
        var player = await db.ReadCreature(_playerId);
        Assert.Equal((player.MaximumHp, true), (player.CurrentHp, await WeatherExists()));
    }

    [Fact]
    public async Task Tick_SkipsTheActivePasses_WhenTheWorldHasNoPlayer()
    {
        // Arrange
        await StartRunner();
        await _runner.Tick(Now, TestContext.Current.CancellationToken);
        var flushesBefore = _dispatcher.FlushedWorldIds.Count;

        // Act
        await _runner.Tick(Now + ActivePassInterval, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(flushesBefore, _dispatcher.FlushedWorldIds.Count);
    }

    private static Task FailingWork(LivePlayerPoses poses, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");

    private sealed class RecordingDispatcher : IGameClientEventDispatcher
    {
        public List<Guid> FlushedWorldIds { get; } = [];

        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
        {
            FlushedWorldIds.Add(worldId);
            return Task.FromResult(false);
        }
    }

    private sealed class SwitchableOptions(WorldSimulationOptions current)
        : IOptions<WorldSimulationOptions>
    {
        public WorldSimulationOptions Current { get; set; } = current;

        public bool IsBroken { get; set; }

        public WorldSimulationOptions Value =>
            IsBroken ? throw new InvalidOperationException("Options are unavailable.") : Current;
    }

    private async Task<bool> WeatherExists()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext.WeatherStates.AnyAsync(
            weather => weather.StateId == _stateId,
            TestContext.Current.CancellationToken
        );
    }

    private async Task AddPlayerAt(
        Location location,
        double x = 0,
        double y = 0,
        int? currentHp = null
    )
    {
        var player = Builders.MakeCreature(
            _worldId,
            locationId: location.Id,
            movementSpeed: 50,
            x: x,
            y: y,
            currentHp: currentHp
        );
        _playerId = player.Id;
        var world = await _context.Worlds.SingleAsync(
            world => world.Id == _worldId,
            TestContext.Current.CancellationToken
        );
        world.PlayerId = player.Id;
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

    private async Task AddResident()
    {
        _context.Creatures.Add(Builders.MakeCreature(_worldId, locationId: _home.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
