using TRPG.Application.Common.Navigation;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public class WorldSimulatorPatrolTests
{
    private const float MovementSpeed = 50;
    private const double MetersPerSecond = InLocationPace.WalkMetersPerSecond;
    private const double LegMeters = 600 * MetersPerSecond;
    private static readonly TimeSpan LegDuration = TimeSpan.FromSeconds(
        LegMeters / MetersPerSecond
    );
    private const double StopMeters = 300 * MetersPerSecond;
    private static readonly TimeSpan StopOffset = TimeSpan.FromSeconds(
        StopMeters / MetersPerSecond
    );
    private static readonly TimeSpan Dwell = TimeSpan.FromSeconds(90);
    private static readonly Point StopPosition = new(12, 34);
    private static readonly GameInstant Morning = At(10, 0);
    private static readonly GameInstant ShiftStart = At(12, 0);
    private static readonly GameInstant ShiftEnd = At(20, 0);

    private readonly Guid _creatureId = Guid.NewGuid();
    private readonly Guid _routeId = Guid.NewGuid();
    private readonly Guid _barracks = Guid.NewGuid();
    private readonly Guid _gate = Guid.NewGuid();
    private readonly Guid _plaza = Guid.NewGuid();

    [Fact]
    public void Step_KeepsCrossingConnectors_PastTheLastLegOfThePatrol()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate);
        simulator.Step(ShiftStart);

        // Act
        var events = simulator.Step(ShiftStart + LegDuration * 4);

        // Assert
        Assert.Equal(
            [_plaza, _gate, _plaza, _gate],
            events.OfType<LocationEntered>().Select(entered => entered.ToLocationId)
        );
    }

    [Fact]
    public void Step_AlwaysNamesTheNextConnector_WhileThePatrolWraps()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate);
        simulator.Step(ShiftStart);

        // Act
        var events = simulator.Step(ShiftStart + LegDuration * 4);

        // Assert
        Assert.All(
            events.OfType<LocationEntered>(),
            entered => Assert.NotNull(entered.NextConnectorId)
        );
    }

    [Fact]
    public void Step_StartsPatrollingAtTheShiftStart_WhenTheGuardIsAlreadyAtTheFirstStop()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate);

        // Act
        var events = simulator.Step(ShiftStart);

        // Assert
        var started = Assert.IsType<JourneyStarted>(Assert.Single(events));
        Assert.Equal((ShiftStart, _gate), (started.At, started.OriginLocationId));
    }

    [Fact]
    public void Step_BeginsTheLoopWithoutCompletingTheJob_WhenTheGuardWalksToTheFirstStop()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _barracks);
        simulator.Step(Morning);

        // Act
        var events = simulator.Step(ShiftStart + LegDuration);

        // Assert
        Assert.Equal(
            [_gate, _plaza],
            events.OfType<LocationEntered>().Select(entered => entered.ToLocationId)
        );
        Assert.Empty(events.OfType<JourneyCompleted>());
    }

    [Fact]
    public void Step_EndsThePatrolAtTheEndOfTheShift_WithoutCrossingAfterIt()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate);
        simulator.Step(ShiftStart);

        // Act
        var events = simulator.Step(ShiftEnd + TimeSpan.FromMinutes(5));

        // Assert
        var ended = Assert.Single(events.OfType<PatrolEnded>());
        Assert.Equal(ShiftEnd, ended.At);
        Assert.Equal(ended, events[^1]);
        Assert.DoesNotContain(events.OfType<LocationEntered>(), entered => entered.At > ShiftEnd);
    }

    [Fact]
    public void Step_StopsTheGuardAtTheirLastCrossing_WhenThePatrolEndsMidLeg()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate);
        simulator.Step(At(12, 5));

        // Act
        simulator.Step(ShiftEnd);

        // Assert
        Assert.Equal(new SimCreatureState(_plaza, false, false), simulator.StateOf(_creatureId));
    }

    [Fact]
    public void Step_SendsTheGuardToTheirSleepJob_AfterThePatrolEnds()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate);
        simulator.Step(ShiftStart);
        simulator.Step(ShiftEnd);
        simulator.Step(At(21, 0));

        // Act
        var events = simulator.Step(At(22, 0));

        // Assert
        var completed = Assert.Single(events.OfType<JourneyCompleted>());
        Assert.Equal(
            (_barracks, CreatureJobAction.Sleep),
            (completed.LocationId, completed.Action)
        );
    }

    [Fact]
    public void Engage_FreezesThePatrol_AndReleaseResumesTheLeg()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate);
        simulator.Step(ShiftStart);
        simulator.Engage(_creatureId, ShiftStart + LegDuration + TimeSpan.FromMinutes(5));
        var frozenEvents = simulator.Step(At(14, 0));
        simulator.Release(_creatureId, At(14, 0));

        // Act
        var events = simulator.Step(At(14, 10));

        // Assert
        Assert.Empty(frozenEvents);
        var entered = Assert.IsType<LocationEntered>(Assert.Single(events));
        Assert.Equal((At(14, 5), _gate), (entered.At, entered.ToLocationId));
    }

    [Fact]
    public void Step_PausesTheGuardAtTheStop_ForThePatrolDwell()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate, withStop: true);
        simulator.Step(ShiftStart);

        // Act
        var events = simulator.Step(ShiftStart + StopOffset + Dwell + LegDuration);

        // Assert
        var dwell = Assert.IsType<DwellStarted>(events[0]);
        Assert.Equal(
            (ShiftStart + StopOffset, _gate, StopPosition),
            (dwell.At, dwell.LocationId, dwell.Position)
        );
        var resumed = Assert.IsType<JourneyStarted>(events[1]);
        Assert.Equal(ShiftStart + StopOffset + Dwell, resumed.At);
        Assert.Null(resumed.StopPosition);
        var entered = Assert.IsType<LocationEntered>(events[2]);
        Assert.Equal(ShiftStart + LegDuration + Dwell, entered.At);
    }

    [Fact]
    public void Step_AimsTheFirstWalkAtTheStop_WhenTheLegHasOne()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate, withStop: true);

        // Act
        var events = simulator.Step(ShiftStart);

        // Assert
        var started = Assert.IsType<JourneyStarted>(Assert.Single(events));
        Assert.Equal(StopPosition, started.StopPosition);
    }

    [Fact]
    public void Release_PushesTheDwellBack_ByTheTimeTheGuardWasEngaged()
    {
        // Arrange
        var simulator = CreateSimulator();
        AddGuard(simulator, _gate, withStop: true);
        simulator.Step(ShiftStart);
        simulator.Step(ShiftStart + StopOffset + TimeSpan.FromSeconds(10));
        simulator.Engage(_creatureId, ShiftStart + StopOffset + TimeSpan.FromSeconds(20));
        var releasedAt = ShiftStart + StopOffset + TimeSpan.FromMinutes(10);
        simulator.Release(_creatureId, releasedAt);

        // Act
        var events = simulator.Step(releasedAt + Dwell);

        // Assert
        var resumed = Assert.IsType<JourneyStarted>(Assert.Single(events));
        Assert.Equal(releasedAt + Dwell - TimeSpan.FromSeconds(20), resumed.At);
    }

    [Fact]
    public void Add_IgnoresAPatrol_WhoseLegsCoverNoDistance()
    {
        // Arrange
        var simulator = CreateSimulator();
        var motionless = BuildPatrol().Select(leg => leg with { Distance = 0 }).ToArray();
        simulator.Add(
            Seed(_gate, new Dictionary<Guid, IReadOnlyList<RouteLeg>> { [_routeId] = motionless }),
            Morning
        );

        // Act
        var events = simulator.Step(ShiftStart);

        // Assert
        Assert.Empty(events);
    }

    private void AddGuard(WorldSimulator simulator, Guid startLocationId, bool withStop = false) =>
        simulator.Add(
            Seed(
                startLocationId,
                new Dictionary<Guid, IReadOnlyList<RouteLeg>>
                {
                    [_routeId] = withStop ? BuildPatrolWithStop() : BuildPatrol(),
                }
            ),
            Morning
        );

    private SimCreatureSeed Seed(
        Guid startLocationId,
        IReadOnlyDictionary<Guid, IReadOnlyList<RouteLeg>> patrols
    ) =>
        new(
            _creatureId,
            startLocationId,
            MovementSpeed,
            [
                Job(CreatureJobAction.Sleep, 20, 12, _barracks, null),
                Job(CreatureJobAction.Work, 12, 20, _gate, _routeId),
            ],
            false,
            patrols
        );

    private CreatureJob Job(
        CreatureJobAction action,
        int startHour,
        int endHour,
        Guid locationId,
        Guid? routeId
    ) =>
        Builders.MakeCreatureJob(
            _creatureId,
            action: action,
            startHour: startHour,
            endHour: endHour,
            locationId: locationId,
            routeId: routeId
        );

    private IReadOnlyList<RouteLeg> BuildPatrolWithStop()
    {
        var legs = BuildPatrol().ToArray();
        legs[0] = legs[0] with { Stop = new RouteStop(StopMeters, StopPosition) };

        return legs;
    }

    private IReadOnlyList<RouteLeg> BuildPatrol() => BuildGraph().BuildCycle([_gate, _plaza]);

    private WorldSimulator CreateSimulator() =>
        new(BuildGraph(), new WorldSimulatorOptions(1, 10, TimeSpan.Zero));

    private TravelGraph BuildGraph()
    {
        var exitBarracks = Node(_barracks);
        var arrivalGateFromBarracks = Node(_gate);
        var exitGate = Node(_gate);
        var arrivalPlazaFromGate = Node(_plaza);
        var exitPlazaToGate = Node(_plaza);
        var exitPlazaToBarracks = Node(_plaza);
        var arrivalGateFromPlaza = Node(_gate);
        var arrivalBarracks = Node(_barracks);

        return new TravelGraph(
            [
                Door(_barracks, exitBarracks, _gate, arrivalGateFromBarracks),
                Walk(_gate, arrivalGateFromBarracks, exitGate),
                Walk(_gate, arrivalGateFromPlaza, exitGate),
                Door(_gate, exitGate, _plaza, arrivalPlazaFromGate),
                Walk(_plaza, arrivalPlazaFromGate, exitPlazaToGate),
                Walk(_plaza, arrivalPlazaFromGate, exitPlazaToBarracks),
                Door(_plaza, exitPlazaToGate, _gate, arrivalGateFromPlaza),
                Door(_plaza, exitPlazaToBarracks, _barracks, arrivalBarracks),
            ],
            [
                exitBarracks,
                arrivalGateFromBarracks,
                exitGate,
                arrivalPlazaFromGate,
                exitPlazaToGate,
                exitPlazaToBarracks,
                arrivalGateFromPlaza,
                arrivalBarracks,
            ]
        );
    }

    private static GameInstant At(int hour, int minute) =>
        new(new DateTime(2000, 1, 3, hour, minute, 0));

    private static TravelNode Node(Guid locationId) => Builders.MakeTravelNode(locationId);

    private static LocationConnector Door(
        Guid originLocationId,
        TravelNode exit,
        Guid destinationLocationId,
        TravelNode arrival
    )
    {
        var connector = Builders.MakeLocationConnector(originLocationId, destinationLocationId);
        connector.OriginNodeId = exit.Id;
        connector.DestinationNodeId = arrival.Id;

        return connector;
    }

    private static PointConnector Walk(Guid locationId, TravelNode from, TravelNode to) =>
        Builders.MakePointConnector(locationId, from.Id, to.Id, LegMeters);
}
