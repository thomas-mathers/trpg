using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class WorldSimulatorLoaderTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 13, 0, 0));

    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WorldSimulatorLoader _loader = null!;
    private Location _home = null!;
    private Location _workplace = null!;
    private LocationConnector _door = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _loader = new WorldSimulatorLoader(
            _serviceProvider.GetRequiredService<
                IQueryHandler<
                    GetSimulatableCreaturesQuery,
                    IReadOnlyCollection<SimulatableCreature>
                >
            >(),
            _serviceProvider.GetRequiredService<
                IQueryHandler<
                    GetCreatureJobsByCreatureIdsQuery,
                    IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
                >
            >(),
            _serviceProvider.GetRequiredService<
                IQueryHandler<GetTravelTopologyQuery, TravelTopology>
            >(),
            _serviceProvider.GetRequiredService<
                IQueryHandler<GetRouteTravelerCreatureIdsByWorldIdQuery, IReadOnlyCollection<Guid>>
            >(),
            _serviceProvider.GetRequiredService<
                IQueryHandler<
                    GetRouteStopsByIdsQuery,
                    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
                >
            >(),
            Options.Create(new WorldClockOptions()),
            Options.Create(new WorldSimulationOptions())
        );

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
    public async Task Load_SimulatesAWorkerTowardTheirJob_WhenTheWindowIsOpen()
    {
        // Arrange
        var worker = await AddWorker();
        var loaded = await _loader.Load(_worldId, Now, TestContext.Current.CancellationToken);
        var events = loaded.Simulator.Step(Now);

        // Act
        var update = Assert.Single(loaded.PoseMapper.Map(events));

        // Assert
        Assert.Equal(
            new CreaturePoseUpdate(
                worker.Id,
                _workplace.Id,
                _home.Id,
                CreatureMovement.Stationary,
                CreatureActivity.Working,
                new WalkColumns(new Point(6, 7), Now, null, null)
            ),
            update
        );
    }

    [Fact]
    public async Task Load_SkipsEngagedAndDeadCreatures()
    {
        // Arrange
        var engaged = await AddWorker(isEngaged: true);
        var dead = await AddWorker(condition: CreatureCondition.Dead);

        // Act
        var loaded = await _loader.Load(_worldId, Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            (null, null),
            (loaded.Simulator.StateOf(engaged.Id), loaded.Simulator.StateOf(dead.Id))
        );
    }

    [Fact]
    public async Task Load_SkipsRestrainedCreatures()
    {
        // Arrange
        var restrained = await AddWorker(isRestrained: true);

        // Act
        var loaded = await _loader.Load(_worldId, Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(loaded.Simulator.StateOf(restrained.Id));
    }

    [Fact]
    public async Task Load_SkipsAPatroller_WhoseRouteHasNoPatrol()
    {
        // Arrange
        var patroller = await AddWorker(routeId: Guid.NewGuid());

        // Act
        var loaded = await _loader.Load(_worldId, Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(loaded.Simulator.StateOf(patroller.Id));
    }

    [Fact]
    public async Task Load_PatrolsTheRoute_WhenTheGuardStartsAtItsFirstStop()
    {
        // Arrange
        var route = await _context.AddPatrolRoute(
            _worldId,
            _door,
            TestContext.Current.CancellationToken
        );
        var patroller = await AddWorker(routeId: route.Id, locationId: _workplace.Id);
        var loaded = await _loader.Load(_worldId, Now, TestContext.Current.CancellationToken);

        // Act
        var events = loaded.Simulator.Step(Now);

        // Assert
        var started = Assert.Single(events.OfType<JourneyStarted>());
        Assert.Equal(
            (patroller.Id, _workplace.Id),
            (started.CreatureId, started.DestinationLocationId)
        );
    }

    [Fact]
    public async Task Load_SkipsRouteTravelers()
    {
        // Arrange
        var traveler = await AddWorker();
        var caravan = Builders.MakeCaravan(Guid.NewGuid(), _worldId);
        _context.RouteTravelers.Add(caravan);
        _context.RouteTravelerMembers.Add(
            Builders.MakeRouteTravelerMember(caravan.Id, traveler.Id, _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var loaded = await _loader.Load(_worldId, Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(loaded.Simulator.StateOf(traveler.Id));
    }

    private async Task<Creature> AddWorker(
        bool isEngaged = false,
        bool isRestrained = false,
        Guid? routeId = null,
        CreatureCondition condition = default,
        Guid? locationId = null
    )
    {
        var worker = Builders.MakeCreature(
            _worldId,
            locationId: locationId ?? _home.Id,
            movementSpeed: 50,
            isEngaged: isEngaged,
            isRestrained: isRestrained,
            condition: condition
        );
        _context.Creatures.Add(worker);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                worker.Id,
                action: CreatureJobAction.Work,
                startHour: 0,
                endHour: 24,
                locationId: _workplace.Id,
                worldId: _worldId,
                routeId: routeId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return worker;
    }
}
