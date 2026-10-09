using TRPG.Application.Creatures.Commands;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public class CreaturePoseMapperTests
{
    private static readonly GameInstant Departure = new(new DateTime(2000, 1, 3, 11, 0, 0));
    private static readonly GameInstant Crossing = Departure + TimeSpan.FromMinutes(10);

    private readonly Guid _creatureId = Guid.NewGuid();
    private readonly Guid _locationA = Guid.NewGuid();
    private readonly Guid _locationB = Guid.NewGuid();
    private readonly Guid _locationC = Guid.NewGuid();
    private readonly LocationConnector _doorAb;
    private readonly LocationConnector _doorBc;
    private readonly CreaturePoseMapper _mapper;

    public CreaturePoseMapperTests()
    {
        _doorAb = Builders.MakeLocationConnector(_locationA, _locationB);
        _doorBc = Builders.MakeLocationConnector(_locationB, _locationC);
        var exitAb = Node(_locationA, 1, 2);
        var arrivalAb = Node(_locationB, 3, 4);
        var exitBc = Node(_locationB, 5, 6);
        var arrivalBc = Node(_locationC, 7, 8);
        _doorAb.OriginNodeId = exitAb.Id;
        _doorAb.DestinationNodeId = arrivalAb.Id;
        _doorBc.OriginNodeId = exitBc.Id;
        _doorBc.DestinationNodeId = arrivalBc.Id;
        _mapper = new CreaturePoseMapper(
            PlacedConnector.Place([_doorAb, _doorBc], [exitAb, arrivalAb, exitBc, arrivalBc])
        );
    }

    [Fact]
    public void MapEvent_PlacesTheCreatureAtTheFirstExit_WhenTheJourneyStarts()
    {
        // Arrange
        var started = new JourneyStarted(
            _creatureId,
            Departure,
            _locationA,
            _locationC,
            _doorAb.Id
        );

        // Act
        var update = _mapper.MapEvent(started);

        // Assert
        Assert.Equal(
            new CreaturePoseUpdate(
                _creatureId,
                _locationA,
                null,
                CreatureMovement.Walking,
                null,
                new WalkColumns(null, null, new Point(1, 2), Departure)
            ),
            update
        );
    }

    [Fact]
    public void MapEvent_RecordsAPassThroughWalk_WhenTheCreatureEntersAnIntermediateLocation()
    {
        // Arrange
        var entered = new LocationEntered(
            _creatureId,
            Crossing,
            _locationA,
            _locationB,
            _doorAb.Id,
            _doorBc.Id
        );

        // Act
        var update = _mapper.MapEvent(entered);

        // Assert
        Assert.Equal(
            new CreaturePoseUpdate(
                _creatureId,
                _locationB,
                _locationA,
                CreatureMovement.Walking,
                null,
                new WalkColumns(new Point(3, 4), Crossing, new Point(5, 6), Crossing)
            ),
            update
        );
    }

    [Fact]
    public void MapEvent_LeavesTheExitOpen_WhenTheCreatureEntersTheFinalLocation()
    {
        // Arrange
        var entered = new LocationEntered(
            _creatureId,
            Crossing,
            _locationB,
            _locationC,
            _doorBc.Id,
            null
        );

        // Act
        var update = _mapper.MapEvent(entered);

        // Assert
        Assert.Equal(new WalkColumns(new Point(7, 8), Crossing, null, null), update.Walk);
    }

    [Fact]
    public void MapEvent_SettlesTheCreature_WhenTheJourneyCompletes()
    {
        // Arrange
        var completed = new JourneyCompleted(
            _creatureId,
            Crossing,
            _locationC,
            Guid.NewGuid(),
            CreatureJobAction.Work
        );

        // Act
        var update = _mapper.MapEvent(completed);

        // Assert
        Assert.Equal(
            new CreaturePoseUpdate(
                _creatureId,
                _locationC,
                null,
                CreatureMovement.Stationary,
                CreatureActivity.Working,
                null
            ),
            update
        );
    }

    [Fact]
    public void Map_KeepsTheFinalEntryPoint_WhenCrossingAndCompletionShareABatch()
    {
        // Arrange
        SimEvent[] events =
        [
            new LocationEntered(_creatureId, Crossing, _locationB, _locationC, _doorBc.Id, null),
            new JourneyCompleted(
                _creatureId,
                Crossing,
                _locationC,
                Guid.NewGuid(),
                CreatureJobAction.Idle
            ),
        ];

        // Act
        var updates = _mapper.Map(events);

        // Assert
        var update = Assert.Single(updates);
        Assert.Equal(
            (
                CreatureMovement.Stationary,
                _locationB,
                new WalkColumns(new Point(7, 8), Crossing, null, null)
            ),
            (update.Movement, update.PreviousLocationId, update.Walk)
        );
    }

    [Fact]
    public void Map_ReturnsTheLatestPoseForEachCreature_WhenSeveralCreaturesMove()
    {
        // Arrange
        var otherId = Guid.NewGuid();
        SimEvent[] events =
        [
            new JourneyStarted(_creatureId, Departure, _locationA, _locationC, _doorAb.Id),
            new JourneyStarted(otherId, Departure, _locationA, _locationC, _doorAb.Id),
            new LocationEntered(
                _creatureId,
                Crossing,
                _locationA,
                _locationB,
                _doorAb.Id,
                _doorBc.Id
            ),
        ];

        // Act
        var updates = _mapper.Map(events);

        // Assert
        Assert.Equal(
            [(_creatureId, _locationB), (otherId, _locationA)],
            updates.Select(update => (update.CreatureId, update.LocationId))
        );
    }

    private static TravelNode Node(Guid locationId, double x, double y) =>
        Builders.MakeTravelNode(locationId, x, y);
}
