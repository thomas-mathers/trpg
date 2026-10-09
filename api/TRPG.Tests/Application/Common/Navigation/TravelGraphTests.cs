using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Common.Navigation;

public class TravelGraphTests
{
    private const double Tolerance = 1e-9;

    private readonly Guid _locationA = Guid.NewGuid();
    private readonly Guid _locationB = Guid.NewGuid();
    private readonly Guid _locationC = Guid.NewGuid();

    [Fact]
    public void FindShortestPath_ReturnsNoLegs_WhenOriginIsDestination()
    {
        // Arrange
        var graph = new TravelGraph([], []);

        // Act
        var legs = graph.FindShortestPath(_locationA, null, _locationA);

        // Assert
        Assert.Empty(legs);
    }

    [Fact]
    public void FindShortestPath_ChargesTheWalkBeforeTheConnector_WhenStartNodeIsGiven()
    {
        // Arrange
        var arrival = Node(_locationA);
        var exit = Node(_locationA);
        var arrivalB = Node(_locationB);
        var door = Door(_locationA, exit, _locationB, arrivalB);
        var graph = new TravelGraph(
            [Walk(_locationA, arrival, exit, 7), door],
            [arrival, exit, arrivalB]
        );

        // Act
        var legs = graph.FindShortestPath(_locationA, arrival.Id, _locationB);

        // Assert
        Assert.Equal(7, Assert.Single(legs).Distance, Tolerance);
    }

    [Fact]
    public void FindShortestPath_StartsFromEveryNodeOfTheOrigin_WhenStartNodeIsNull()
    {
        // Arrange
        var exit = Node(_locationA);
        var arrival = Node(_locationB);
        var door = Door(_locationA, exit, _locationB, arrival);
        var graph = new TravelGraph([door], [exit, arrival]);

        // Act
        var legs = graph.FindShortestPath(_locationA, null, _locationB);

        // Assert
        Assert.Equal(door.Id, Assert.Single(legs).ConnectorId);
    }

    [Fact]
    public void FindShortestPath_TakesTheCheaperExit_WhenTwoDoorsLeadToTheDestination()
    {
        // Arrange
        var arrival = Node(_locationA);
        var farExit = Node(_locationA);
        var nearExit = Node(_locationA);
        var farArrival = Node(_locationB);
        var nearArrival = Node(_locationB);
        var farDoor = Door(_locationA, farExit, _locationB, farArrival);
        var nearDoor = Door(_locationA, nearExit, _locationB, nearArrival);
        var graph = new TravelGraph(
            [
                Walk(_locationA, arrival, farExit, 10),
                Walk(_locationA, arrival, nearExit, 3),
                farDoor,
                nearDoor,
            ],
            [arrival, farExit, nearExit, farArrival, nearArrival]
        );

        // Act
        var legs = graph.FindShortestPath(_locationA, arrival.Id, _locationB);

        // Assert
        Assert.Equal(nearDoor.Id, Assert.Single(legs).ConnectorId);
    }

    [Fact]
    public void FindShortestPath_ChargesEachLocationsWalkToItsOwnLeg_WhenRouteCrossesThreeLocations()
    {
        // Arrange
        var exitA = Node(_locationA);
        var arrivalB = Node(_locationB);
        var exitB = Node(_locationB);
        var arrivalC = Node(_locationC);
        var graph = new TravelGraph(
            [
                Door(_locationA, exitA, _locationB, arrivalB),
                Walk(_locationB, arrivalB, exitB, 4),
                Door(_locationB, exitB, _locationC, arrivalC),
            ],
            [exitA, arrivalB, exitB, arrivalC]
        );

        // Act
        var legs = graph.FindShortestPath(_locationA, null, _locationC);

        // Assert
        Assert.Equal([0d, 4d], legs.Select(leg => leg.Distance));
    }

    [Fact]
    public void FindShortestPath_ReturnsNoLegs_WhenDestinationIsUnreachable()
    {
        // Arrange
        var exit = Node(_locationA);
        var arrival = Node(_locationB);
        var graph = new TravelGraph([Door(_locationA, exit, _locationB, arrival)], [exit, arrival]);

        // Act
        var legs = graph.FindShortestPath(_locationB, null, _locationA);

        // Assert
        Assert.Empty(legs);
    }

    [Fact]
    public void FindShortestPath_FollowsBothDirections_WhenPointConnectorIsBidirectional()
    {
        // Arrange
        var arrival = Node(_locationA);
        var exit = Node(_locationA);
        var arrivalB = Node(_locationB);
        var door = Door(_locationA, exit, _locationB, arrivalB);
        var graph = new TravelGraph(
            [Walk(_locationA, exit, arrival, 6, bidirectional: true), door],
            [arrival, exit, arrivalB]
        );

        // Act
        var legs = graph.FindShortestPath(_locationA, arrival.Id, _locationB);

        // Assert
        Assert.Equal(6, Assert.Single(legs).Distance, Tolerance);
    }

    [Fact]
    public void FindShortestPath_ReturnsNoLegs_WhenPointConnectorIsOneWayAgainstTheWalk()
    {
        // Arrange
        var arrival = Node(_locationA);
        var exit = Node(_locationA);
        var door = Door(_locationA, exit, _locationB, Node(_locationB));
        var graph = new TravelGraph([Walk(_locationA, exit, arrival, 6), door], [arrival, exit]);

        // Act
        var legs = graph.FindShortestPath(_locationA, arrival.Id, _locationB);

        // Assert
        Assert.Empty(legs);
    }

    [Fact]
    public void FindNearestLocation_ReturnsOrigin_WhenOriginIsACandidate()
    {
        // Arrange
        var graph = new TravelGraph([], []);

        // Act
        var nearest = graph.FindNearestLocation(_locationA, new HashSet<Guid> { _locationA });

        // Assert
        Assert.Equal(_locationA, nearest);
    }

    [Fact]
    public void FindNearestLocation_ReturnsTheCandidateWithTheShortestWalk()
    {
        // Arrange
        var arrival = Node(_locationA);
        var exitToB = Node(_locationA);
        var exitToC = Node(_locationA);
        var arrivalB = Node(_locationB);
        var arrivalC = Node(_locationC);
        var graph = new TravelGraph(
            [
                Walk(_locationA, arrival, exitToB, 10),
                Walk(_locationA, arrival, exitToC, 3),
                Door(_locationA, exitToB, _locationB, arrivalB),
                Door(_locationA, exitToC, _locationC, arrivalC),
            ],
            [arrival, exitToB, exitToC, arrivalB, arrivalC]
        );

        // Act
        var nearest = graph.FindNearestLocation(
            _locationA,
            new HashSet<Guid> { _locationB, _locationC }
        );

        // Assert
        Assert.Equal(_locationC, nearest);
    }

    [Fact]
    public void FindNearestLocation_ReturnsNull_WhenNoCandidateIsReachable()
    {
        // Arrange
        var exit = Node(_locationA);
        var arrival = Node(_locationB);
        var graph = new TravelGraph([Door(_locationA, exit, _locationB, arrival)], [exit, arrival]);

        // Act
        var nearest = graph.FindNearestLocation(_locationA, new HashSet<Guid> { _locationC });

        // Assert
        Assert.Null(nearest);
    }

    [Fact]
    public void ShortestDistance_SumsPointConnectors_WhenNodesAreJoinedInSequence()
    {
        // Arrange
        var first = Node(_locationA);
        var middle = Node(_locationA);
        var last = Node(_locationA);
        var graph = new TravelGraph(
            [Walk(_locationA, first, middle, 2.5), Walk(_locationA, middle, last, 4)],
            [first, middle, last]
        );

        // Act
        var distance = graph.ShortestDistance(first.Id, last.Id);

        // Assert
        Assert.Equal(6.5, distance, Tolerance);
    }

    [Fact]
    public void ShortestDistance_ReturnsZero_WhenNodesAreTheSame()
    {
        // Arrange
        var node = Node(_locationA);
        var graph = new TravelGraph([], [node]);

        // Act
        var distance = graph.ShortestDistance(node.Id, node.Id);

        // Assert
        Assert.Equal(0, distance, Tolerance);
    }

    [Fact]
    public void ArrivalNodeOf_ReturnsTheDestinationNodeOfTheConnector()
    {
        // Arrange
        var arrival = Node(_locationB);
        var door = Door(_locationA, Node(_locationA), _locationB, arrival);
        var graph = new TravelGraph([door], []);

        // Act
        var arrivalNodeId = graph.ArrivalNodeOf(door.Id);

        // Assert
        Assert.Equal(arrival.Id, arrivalNodeId);
    }

    [Fact]
    public void OriginNodeOf_ReturnsTheOriginNodeOfTheConnector()
    {
        // Arrange
        var exit = Node(_locationA);
        var door = Door(_locationA, exit, _locationB, Node(_locationB));
        var graph = new TravelGraph([door], []);

        // Act
        var originNodeId = graph.OriginNodeOf(door.Id);

        // Assert
        Assert.Equal(exit.Id, originNodeId);
    }

    [Fact]
    public void WhereLocation_DropsConnectorsTouchingExcludedLocations()
    {
        // Arrange
        var exitA = Node(_locationA);
        var arrivalB = Node(_locationB);
        var exitB = Node(_locationB);
        var arrivalC = Node(_locationC);
        var graph = new TravelGraph(
            [
                Door(_locationA, exitA, _locationB, arrivalB),
                Walk(_locationB, arrivalB, exitB, 4),
                Door(_locationB, exitB, _locationC, arrivalC),
            ],
            [exitA, arrivalB, exitB, arrivalC]
        );

        // Act
        var filtered = graph.WhereLocation(locationId => locationId != _locationC);

        // Assert
        Assert.Empty(filtered.FindShortestPath(_locationA, null, _locationC));
    }

    [Fact]
    public void WhereLocation_KeepsConnectorsBetweenIncludedLocations()
    {
        // Arrange
        var exitA = Node(_locationA);
        var arrivalB = Node(_locationB);
        var graph = new TravelGraph(
            [Door(_locationA, exitA, _locationB, arrivalB)],
            [exitA, arrivalB]
        );

        // Act
        var filtered = graph.WhereLocation(locationId => locationId != _locationC);

        // Assert
        Assert.Single(filtered.FindShortestPath(_locationA, null, _locationB));
    }

    private TravelGraph BuildSpurGraph(
        out TravelNode hub,
        out TravelNode spurStop,
        out TravelNode stranded
    )
    {
        hub = Node(_locationA);
        var portA = Node(_locationA);
        var arrivalA = Node(_locationA);
        var arrivalB = Node(_locationB);
        spurStop = Node(_locationB);
        var exitB = Node(_locationB);
        stranded = Node(_locationC);

        return new TravelGraph(
            [
                Door(_locationA, portA, _locationB, arrivalB),
                Door(_locationB, exitB, _locationA, arrivalA),
                Walk(_locationA, hub, portA, 5, bidirectional: true),
                Walk(_locationA, arrivalA, hub, 3, bidirectional: true),
                Walk(_locationB, arrivalB, spurStop, 4, bidirectional: true),
                Walk(_locationB, spurStop, exitB, 4, bidirectional: true),
            ],
            [hub, portA, arrivalA, arrivalB, spurStop, exitB, stranded]
        );
    }

    private TravelGraph BuildOutAndBackGraph()
    {
        var exitA = Node(_locationA);
        var arrivalA = Node(_locationA);
        var arrivalB = Node(_locationB);
        var exitB = Node(_locationB);
        var outbound = Door(_locationA, exitA, _locationB, arrivalB);
        var inbound = Door(_locationB, exitB, _locationA, arrivalA);

        return new TravelGraph(
            [
                outbound,
                inbound,
                Walk(_locationB, arrivalB, exitB, 4),
                Walk(_locationA, arrivalA, exitA, 9),
            ],
            [exitA, arrivalA, arrivalB, exitB]
        );
    }

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

    private static PointConnector Walk(
        Guid locationId,
        TravelNode from,
        TravelNode to,
        double distance,
        bool bidirectional = false
    ) =>
        Builders.MakePointConnector(
            locationId,
            from.Id,
            to.Id,
            distance,
            bidirectional: bidirectional
        );
}
