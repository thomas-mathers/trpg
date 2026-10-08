using TRPG.Application.Common.Navigation;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public class WorldSimulatorTests
{
    private const float MovementSpeed = 50;
    private const double MetersPerSecond = 3;
    private const double LegMeters = 1800;
    private static readonly TimeSpan LegDuration = TimeSpan.FromSeconds(
        LegMeters / MetersPerSecond
    );
    private static readonly GameInstant Morning = At(10, 0);
    private static readonly GameInstant WorkStart = At(12, 0);

    private readonly Guid _creatureId = Guid.NewGuid();
    private readonly Guid _locationA = Guid.NewGuid();
    private readonly Guid _locationB = Guid.NewGuid();
    private readonly Guid _locationC = Guid.NewGuid();
    private readonly Guid _locationD = Guid.NewGuid();

    [Fact]
    public void Step_StartsTheJourneyAtWindowStartMinusWalkDuration()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddCommuter(simulator);
        simulator.Step(Morning);

        // Act
        var events = simulator.Step(WorkStart - LegDuration);

        // Assert
        var started = Assert.IsType<JourneyStarted>(events[0]);
        Assert.Equal(WorkStart - LegDuration, started.At);
        Assert.Equal(_locationC, started.DestinationLocationId);
    }

    [Fact]
    public void Step_EmitsNothing_BeforeTheDeparture()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddCommuter(simulator);
        simulator.Step(Morning);

        // Act
        var events = simulator.Step(WorkStart - LegDuration - TimeSpan.FromSeconds(1));

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void Step_CompletesTheJourneyExactlyAtWindowStart()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddCommuter(simulator);
        simulator.Step(Morning);
        simulator.Step(WorkStart - LegDuration);

        // Act
        var events = simulator.Step(WorkStart + TimeSpan.FromMinutes(5));

        // Assert
        var completed = Assert.Single(events.OfType<JourneyCompleted>());
        Assert.Equal(WorkStart, completed.At);
        Assert.Equal(_locationC, completed.LocationId);
    }

    [Fact]
    public void Step_DepartsImmediately_WhenTheWindowIsAlreadyOpen()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddCommuter(simulator);

        // Act
        var events = simulator.Step(At(13, 0));

        // Assert
        var started = Assert.IsType<JourneyStarted>(events[0]);
        Assert.Equal(At(13, 0), started.At);
    }

    [Fact]
    public void Step_CarriesTimeAcrossLegs_WhenOneStepSpansSeveralCrossings()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddFarCommuter(simulator);
        simulator.Step(Morning);
        var departure = WorkStart - LegDuration * 2;

        // Act
        var events = simulator.Step(WorkStart);

        // Assert
        var crossings = events.OfType<LocationEntered>().ToArray();
        Assert.Equal(
            [departure, departure + LegDuration, departure + LegDuration * 2],
            crossings.Select(crossing => crossing.At)
        );
        Assert.Equal(
            [_locationB, _locationC, _locationD],
            crossings.Select(crossing => crossing.ToLocationId)
        );
    }

    [Fact]
    public void Engage_ReturnsCrossingsUpToTheEngagementInstant_AndFreezes()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddFarCommuter(simulator);
        simulator.Step(Morning);
        var departure = WorkStart - LegDuration * 2;
        simulator.Step(departure);

        // Act
        var events = simulator.Engage(_creatureId, departure + TimeSpan.FromMinutes(15));

        // Assert
        var crossing = Assert.IsType<LocationEntered>(Assert.Single(events));
        Assert.Equal(departure + LegDuration, crossing.At);
        Assert.True(simulator.StateOf(_creatureId)!.IsFrozen);
    }

    [Fact]
    public void Step_LeavesFrozenCreaturesUntouched()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddCommuter(simulator);
        simulator.Step(Morning);
        simulator.Step(WorkStart - LegDuration);
        simulator.Engage(_creatureId, WorkStart - LegDuration + TimeSpan.FromMinutes(1));

        // Act
        var events = simulator.Step(WorkStart + TimeSpan.FromHours(1));

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void Release_ResumesTheWalkFromWhereItFroze()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddCommuter(simulator);
        simulator.Step(Morning);
        var departure = WorkStart - LegDuration;
        simulator.Step(departure);
        simulator.Engage(_creatureId, departure + TimeSpan.FromMinutes(4));
        var releasedAt = WorkStart + TimeSpan.FromHours(1);
        simulator.Release(_creatureId, releasedAt);

        // Act
        var events = simulator.Step(releasedAt + TimeSpan.FromHours(1));

        // Assert
        var completed = Assert.Single(events.OfType<JourneyCompleted>());
        Assert.Equal(releasedAt + TimeSpan.FromMinutes(6), completed.At);
    }

    [Fact]
    public void Release_ReplansFromScratch_WhenTheCreatureWasNotWalkingWhenEngaged()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddCommuter(simulator);
        simulator.Step(Morning);
        simulator.Engage(_creatureId, Morning);
        simulator.Release(_creatureId, At(13, 0));

        // Act
        var events = simulator.Step(At(13, 0));

        // Assert
        var started = Assert.IsType<JourneyStarted>(events[0]);
        Assert.Equal(At(13, 0), started.At);
    }

    [Fact]
    public void Step_DefersCreaturesPastThePathfindCap_InsteadOfDroppingThem()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        AddAlwaysWorking(simulator, first, _locationB);
        AddAlwaysWorking(simulator, second, _locationC);

        // Act
        var firstStep = simulator.Step(Morning);
        var secondStep = simulator.Step(Morning + TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(
            [first, second],
            firstStep
                .Concat(secondStep)
                .OfType<JourneyStarted>()
                .Select(started => started.CreatureId)
        );
    }

    [Fact]
    public void Step_ServesSharedRoutesFromCache_WithoutSpendingTheCap()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddAlwaysWorking(simulator, Guid.NewGuid(), _locationC);
        AddAlwaysWorking(simulator, Guid.NewGuid(), _locationC);

        // Act
        var events = simulator.Step(Morning);

        // Assert
        Assert.Equal(2, events.OfType<JourneyStarted>().Count());
    }

    [Fact]
    public void Step_ArrivalMatchesTheDeadReckonedFinish_ForTheSameWalk()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddAlwaysWorking(simulator, _creatureId, _locationD);
        simulator.Step(Morning);
        var metersPerGameSecond = InLocationPace.MetersPerGameSecond(MovementSpeed, 1);
        var clientFinish = Morning + TimeSpan.FromSeconds(2 * LegMeters / metersPerGameSecond);

        // Act
        var events = simulator.Step(Morning + TimeSpan.FromDays(1));

        // Assert
        Assert.Equal(clientFinish, Assert.Single(events.OfType<JourneyCompleted>()).At);
    }

    [Fact]
    public void Step_WaitsAtTheDestination_WhenItArrivesBeforeTheWindowOpens()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        var jobs = new[]
        {
            Job(CreatureJobAction.Sleep, 20, 12, _locationA),
            Job(CreatureJobAction.Eat, 12, 13, _locationC),
        };
        var earlyId = FindCreatureDepartingEarly(jobs);
        simulator.Add(new SimCreatureSeed(earlyId, _locationA, MovementSpeed, jobs), Morning);
        simulator.Step(Morning);
        simulator.Step(WorkStart - TimeSpan.FromMinutes(1));

        // Act
        var events = simulator.Step(WorkStart - TimeSpan.FromSeconds(30));

        // Assert
        Assert.DoesNotContain(events, simulatorEvent => simulatorEvent is JourneyStarted);
        Assert.Equal(_locationC, simulator.StateOf(earlyId)!.LocationId);
    }

    [Fact]
    public void Step_KeepsAnIdlerHome_WhenTheirIdleLocationIsExposed()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddIdler(simulator, _locationA, _locationC, _locationA);
        simulator.SetExposedLocations(new HashSet<Guid> { _locationC }, At(13, 0));

        // Act
        var events = simulator.Step(At(13, 0));

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void Step_SendsAnIdlerToTheirIdleLocation_WhenItIsNotExposed()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddIdler(simulator, _locationA, _locationC, _locationA);

        // Act
        var events = simulator.Step(At(13, 0));

        // Assert
        var started = Assert.IsType<JourneyStarted>(events[0]);
        Assert.Equal(_locationC, started.DestinationLocationId);
    }

    [Fact]
    public void Step_SendsAGuardToTheirIdleLocation_WhenItIsExposed()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddIdler(simulator, _locationA, _locationC, _locationA, seeksShelter: false);
        simulator.SetExposedLocations(new HashSet<Guid> { _locationC }, At(13, 0));

        // Act
        var events = simulator.Step(At(13, 0));

        // Assert
        var started = Assert.IsType<JourneyStarted>(events[0]);
        Assert.Equal(_locationC, started.DestinationLocationId);
    }

    [Fact]
    public void Step_SendsAnIdlerHome_WhenTheWeatherTurnsWhileTheyAreAtTheirIdleLocation()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        AddIdler(simulator, _locationA, _locationA, _locationC);
        simulator.Step(At(13, 0));
        simulator.SetExposedLocations(new HashSet<Guid> { _locationA }, At(13, 5));

        // Act
        var events = simulator.Step(At(13, 5));

        // Assert
        var started = Assert.IsType<JourneyStarted>(events[0]);
        Assert.Equal(_locationC, started.DestinationLocationId);
    }

    private void AddIdler(
        WorldSimulator simulator,
        Guid startLocationId,
        Guid idleLocationId,
        Guid sleepLocationId,
        bool seeksShelter = true
    )
    {
        var jobs = new[]
        {
            Job(CreatureJobAction.Sleep, 20, 12, sleepLocationId),
            Job(CreatureJobAction.Idle, 12, 20, idleLocationId),
        };
        simulator.Add(
            new SimCreatureSeed(_creatureId, startLocationId, MovementSpeed, jobs, seeksShelter),
            Morning
        );
    }

    private WorldSimulator CreateSimulator(int searchesPerTick) =>
        new(BuildGraph(), new WorldSimulatorOptions(1, searchesPerTick));

    private void AddCommuter(WorldSimulator simulator) => AddWorker(simulator, _locationC);

    private void AddFarCommuter(WorldSimulator simulator) => AddWorker(simulator, _locationD);

    private void AddWorker(WorldSimulator simulator, Guid workLocationId)
    {
        var jobs = new[]
        {
            Job(CreatureJobAction.Sleep, 20, 12, _locationA),
            Job(CreatureJobAction.Work, 12, 20, workLocationId),
        };
        simulator.Add(new SimCreatureSeed(_creatureId, _locationA, MovementSpeed, jobs), Morning);
    }

    private void AddAlwaysWorking(WorldSimulator simulator, Guid creatureId, Guid workLocationId) =>
        simulator.Add(
            new SimCreatureSeed(
                creatureId,
                _locationA,
                MovementSpeed,
                [Job(CreatureJobAction.Work, 0, 24, workLocationId)]
            ),
            Morning
        );

    private static Guid FindCreatureDepartingEarly(CreatureJob[] jobs)
    {
        var transition = JobTransition.FindNext(jobs, jobs[0].LocationId, Morning, job => job)!;
        for (var seed = 1; ; seed++)
        {
            var id = new Guid(seed, 0, 0, new byte[8]);
            var departure = DepartureTiming.Resolve(id, transition, LegDuration);
            if (departure < transition.At - LegDuration - TimeSpan.FromMinutes(5))
            {
                return id;
            }
        }
    }

    private CreatureJob Job(
        CreatureJobAction action,
        int startHour,
        int endHour,
        Guid locationId
    ) =>
        Builders.MakeCreatureJob(
            _creatureId,
            action: action,
            startHour: startHour,
            endHour: endHour,
            locationId: locationId
        );

    private TravelGraph BuildGraph()
    {
        var exitA = Node(_locationA);
        var arrivalB = Node(_locationB);
        var exitB = Node(_locationB);
        var arrivalC = Node(_locationC);
        var exitC = Node(_locationC);
        var arrivalD = Node(_locationD);

        return new TravelGraph(
            [
                Door(_locationA, exitA, _locationB, arrivalB),
                Walk(_locationB, arrivalB, exitB),
                Door(_locationB, exitB, _locationC, arrivalC),
                Walk(_locationC, arrivalC, exitC),
                Door(_locationC, exitC, _locationD, arrivalD),
            ],
            [exitA, arrivalB, exitB, arrivalC, exitC, arrivalD]
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
