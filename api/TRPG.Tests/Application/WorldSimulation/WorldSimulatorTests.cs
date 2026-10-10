using TRPG.Application.Common.Navigation;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public class WorldSimulatorTests
{
    private const float MovementSpeed = 50;
    private const double MetersPerSecond = InLocationPace.WalkMetersPerSecond;
    private const double LegMeters = 600 * MetersPerSecond;
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
    public void SleepUntilNextRoutineChange_WakesAtTheCurrentJobsEnd()
    {
        // Arrange
        var simulator = CreateSimulator(1);
        simulator.Add(
            new SimCreatureSeed(
                _creatureId,
                _locationA,
                MovementSpeed,
                [
                    Job(CreatureJobAction.Sleep, 20, 12, _locationA),
                    Job(CreatureJobAction.Work, 12, 20, _locationC),
                ]
            ),
            Morning
        );

        // Act
        simulator.SleepUntilNextRoutineChange(_creatureId, Morning);

        // Assert
        var state = simulator.StateOf(_creatureId)!;
        Assert.True(simulator.IsSchedulerSleeping(_creatureId, Morning));
        Assert.Equal(WorkStart, state.NextUpdateAt);
    }

    [Fact]
    public void Step_CompletesATransientLocalMoveWithoutCreatingAJourney()
    {
        var simulator = CreateSimulator(1);
        simulator.Add(new SimCreatureSeed(_creatureId, _locationA, MovementSpeed, []), Morning);
        var move = new LocalMovePlan(
            _creatureId,
            _locationA,
            Guid.NewGuid(),
            CreatureJobAction.Work,
            LocalMoveTargetKind.Workstation,
            Guid.NewGuid(),
            [new Point(1, 1), new Point(4, 1)]
        );
        var started = Assert.Single(simulator.StartLocalMove(move, Morning));

        var events = simulator.Step(Morning + TimeSpan.FromHours(1));

        Assert.IsType<LocalMoveStarted>(started);
        var completed = Assert.Single(events.OfType<LocalMoveCompleted>());
        Assert.Equal(new Point(4, 1), completed.StopPosition);
        Assert.False(simulator.StateOf(_creatureId)!.IsWalking);
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

    private WorldSimulator CreateSimulator(int searchesPerTick) =>
        new(new TravelGraph([], []), new WorldSimulatorOptions(1, searchesPerTick, TimeSpan.Zero));

    private void AddCommuter(WorldSimulator simulator) => AddWorker(simulator, _locationC);

    private void AddFarCommuter(WorldSimulator simulator) => AddWorker(simulator, _locationD);

    private void AddWorker(WorldSimulator simulator, Guid workLocationId)
    {
        var jobs = new[]
        {
            Job(CreatureJobAction.Sleep, 20, 12, _locationA),
            Job(CreatureJobAction.Work, 12, 20, workLocationId),
        };
        var duration = workLocationId == _locationD ? LegDuration * 2 : LegDuration;
        simulator.Add(
            new SimCreatureSeed(
                _creatureId,
                _locationA,
                MovementSpeed,
                jobs,
                Journey: JourneyTo(workLocationId, WorkStart - duration, jobs[1])
            ),
            Morning
        );
    }

    private void AddAlwaysWorking(WorldSimulator simulator, Guid creatureId, Guid workLocationId)
    {
        var job = Builders.MakeCreatureJob(
            creatureId,
            action: CreatureJobAction.Work,
            startHour: 0,
            endHour: 24,
            locationId: workLocationId
        );
        simulator.Add(
            new SimCreatureSeed(
                creatureId,
                _locationA,
                MovementSpeed,
                [job],
                Journey: JourneyTo(workLocationId, Morning, job)
            ),
            Morning
        );
    }

    private JourneyExecutionSeed JourneyTo(
        Guid destinationLocationId,
        GameInstant departureAt,
        CreatureJob destinationJob
    )
    {
        var locationsByNodeId = new Dictionary<Guid, Guid>();
        var legs = new List<JourneyLeg>();
        var current = AddNode(_locationA);

        AddLeg(_locationB, 0);
        AddLeg(_locationB, LegMeters);
        AddLeg(_locationC, 0);
        if (destinationLocationId == _locationD)
        {
            AddLeg(_locationC, LegMeters);
            AddLeg(_locationD, 0);
        }

        return new JourneyExecutionSeed(
            Guid.NewGuid(),
            JourneyStatus.Planned,
            departureAt,
            0,
            0,
            Morning,
            legs,
            destinationJob,
            locationsByNodeId
        );

        Guid AddNode(Guid locationId)
        {
            var nodeId = Guid.NewGuid();
            locationsByNodeId[nodeId] = locationId;
            return nodeId;
        }

        void AddLeg(Guid locationId, double distance)
        {
            var next = AddNode(locationId);
            legs.Add(
                new JourneyLeg
                {
                    FromNodeId = current,
                    ToNodeId = next,
                    ConnectorId = Guid.NewGuid(),
                    Distance = distance,
                    Path = new Polyline
                    {
                        Points =
                            distance == 0
                                ? [new Point(0, 0)]
                                : [new Point(0, 0), new Point(distance, 0)],
                    },
                }
            );
            current = next;
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

    private static GameInstant At(int hour, int minute) =>
        new(new DateTime(2000, 1, 3, hour, minute, 0));
}
