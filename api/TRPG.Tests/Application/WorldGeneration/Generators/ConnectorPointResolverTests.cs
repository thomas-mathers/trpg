using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class ConnectorPointResolverTests
{
    private static readonly Footprint Frame = new(Width: 20, Depth: 10);

    private static ConnectorExitRequest Request(
        ConnectorExitKind kind,
        CompassDirection direction = CompassDirection.North,
        double bearingRadians = 0,
        Guid? destinationLocationId = null
    ) =>
        new(Guid.NewGuid(), destinationLocationId ?? Guid.NewGuid(), kind)
        {
            Direction = direction,
            BearingRadians = bearingRadians,
        };

    private static ConnectorExit ExitFor(
        Footprint frame,
        ConnectorExitRequest request,
        params ConnectorExitRequest[] others
    ) =>
        ConnectorPointResolver
            .ResolveExits(frame, [request, .. others])
            .Single(exit => exit.ConnectorId == request.ConnectorId);

    [Theory]
    [InlineData(CompassDirection.North, 10, 0)]
    [InlineData(CompassDirection.South, 10, 10)]
    [InlineData(CompassDirection.East, 20, 5)]
    [InlineData(CompassDirection.West, 0, 5)]
    public void ResolveExits_PlacesACardinalExitAtTheEdgeCenter(
        CompassDirection direction,
        double expectedX,
        double expectedY
    )
    {
        // Arrange
        var request = Request(ConnectorExitKind.Compass, direction);

        // Act
        var exit = ExitFor(Frame, request);

        // Assert
        Assert.Equal(new PlanarPoint(expectedX, expectedY), exit.Point);
    }

    [Theory]
    [InlineData(CompassDirection.North, Math.PI)]
    [InlineData(CompassDirection.South, 0)]
    public void ResolveArrival_StandsOneMeterInsideFacingInward_ForANorthOrSouthExit(
        CompassDirection direction,
        double expectedFacing
    )
    {
        // Arrange
        var exit = ExitFor(Frame, Request(ConnectorExitKind.Compass, direction));

        // Act
        var arrival = ConnectorPointResolver.ResolveArrival(exit);

        // Assert
        Assert.Equal(expectedFacing, arrival.Angle, precision: 9);
        Assert.Equal(1, Math.Abs(arrival.Y - exit.Point.Y), precision: 9);
        Assert.Equal(exit.Point.X, arrival.X, precision: 9);
    }

    [Theory]
    [InlineData(CompassDirection.East, 19, 5)]
    [InlineData(CompassDirection.West, 1, 5)]
    public void ResolveArrival_StandsOneMeterInsideAnEastOrWestExit(
        CompassDirection direction,
        double expectedX,
        double expectedY
    )
    {
        // Arrange
        var exit = ExitFor(Frame, Request(ConnectorExitKind.Compass, direction));

        // Act
        var arrival = ConnectorPointResolver.ResolveArrival(exit);

        // Assert
        Assert.Equal(expectedX, arrival.X, precision: 9);
        Assert.Equal(expectedY, arrival.Y, precision: 9);
    }

    [Fact]
    public void ResolveArrival_FacesInwardFromTheEastEdge_TowardWest()
    {
        // Arrange
        var exit = ExitFor(Frame, Request(ConnectorExitKind.Compass, CompassDirection.East));

        // Act
        var arrival = ConnectorPointResolver.ResolveArrival(exit);

        // Assert
        Assert.Equal(3 * Math.PI / 2, arrival.Angle, precision: 9);
    }

    [Fact]
    public void ResolveExits_PairsOppositeDirectionsAcrossTheirFrames()
    {
        // Arrange
        var northExit = ExitFor(Frame, Request(ConnectorExitKind.Compass, CompassDirection.North));
        var southExit = ExitFor(Frame, Request(ConnectorExitKind.Compass, CompassDirection.South));

        // Act
        var arrivalFromNorth = ConnectorPointResolver.ResolveArrival(southExit);
        var arrivalFromSouth = ConnectorPointResolver.ResolveArrival(northExit);

        // Assert
        Assert.True(arrivalFromNorth.Y < southExit.Point.Y);
        Assert.True(arrivalFromSouth.Y > northExit.Point.Y);
    }

    [Fact]
    public void ResolveExits_SpreadsConnectorsSharingAnEdgeByDestinationId()
    {
        // Arrange
        var first = Request(
            ConnectorExitKind.Compass,
            destinationLocationId: new Guid(1, 0, 0, new byte[8])
        );
        var second = Request(
            ConnectorExitKind.Compass,
            destinationLocationId: new Guid(2, 0, 0, new byte[8])
        );

        // Act
        var exits = ConnectorPointResolver.ResolveExits(Frame, [second, first]);

        // Assert
        var firstPoint = exits.Single(exit => exit.ConnectorId == first.ConnectorId).Point;
        var secondPoint = exits.Single(exit => exit.ConnectorId == second.ConnectorId).Point;
        Assert.Equal(new PlanarPoint(20.0 / 3, 0), firstPoint);
        Assert.Equal(new PlanarPoint(40.0 / 3, 0), secondPoint);
    }

    [Fact]
    public void ResolveExits_PlacesADiagonalExitInsideItsCorner()
    {
        // Arrange
        var request = Request(ConnectorExitKind.Compass, CompassDirection.Northeast);

        // Act
        var exit = ExitFor(Frame, request);

        // Assert
        Assert.Equal(new PlanarPoint(19.5, 0.5), exit.Point);
        Assert.Equal(5 * Math.PI / 4, exit.FacingAngle, precision: 9);
    }

    [Fact]
    public void ResolveExits_PlacesADoorOnTheSouthWallCenterFacingNorth()
    {
        // Arrange
        var request = Request(ConnectorExitKind.SouthDoor);

        // Act
        var exit = ExitFor(Frame, request);

        // Assert
        Assert.Equal(new PlanarPoint(10, 10), exit.Point);
        Assert.Equal(0, exit.FacingAngle);
    }

    [Fact]
    public void ResolveExits_PlacesAStairsExitOnTheNorthWallAroundTheCenter()
    {
        // Arrange
        var request = new ConnectorExitRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ConnectorExitKind.Stairs
        )
        {
            LowerFloorNumber = 0,
        };

        // Act
        var exit = ExitFor(Frame, request);

        // Assert
        Assert.Equal(new PlanarPoint(10 - StairPlan.FlightSpacing / 2, 0), exit.Point);
    }

    [Fact]
    public void ResolveExits_PutsTheUpFlightOnTheOppositeWall_WhenARoomHasFlightsBothWays()
    {
        // Arrange
        var hallway = new Footprint(Width: 2.5, Depth: 24);
        var down = StairsRequest(lowerFloorNumber: 0, StairDirection.Down);
        var up = StairsRequest(lowerFloorNumber: 1, StairDirection.Up);

        // Act
        var exits = ConnectorPointResolver.ResolveExits(hallway, [down, up]);

        // Assert
        var downExit = exits.Single(exit => exit.ConnectorId == down.ConnectorId);
        var upExit = exits.Single(exit => exit.ConnectorId == up.ConnectorId);
        Assert.Equal(0, downExit.Point.Y);
        Assert.Equal(hallway.Depth, upExit.Point.Y);
        Assert.Equal(0, upExit.FacingAngle);
    }

    [Fact]
    public void ResolveExits_AlignsBothFlightDoorsOnTheCenterLine_WhenARoomHasFlightsBothWays()
    {
        // Arrange
        var hallway = new Footprint(Width: 3, Depth: 24);
        var down = StairsRequest(lowerFloorNumber: 0, StairDirection.Down);
        var up = StairsRequest(lowerFloorNumber: 1, StairDirection.Up);

        // Act
        var exits = ConnectorPointResolver.ResolveExits(hallway, [down, up]);

        // Assert
        var downExit = exits.Single(exit => exit.ConnectorId == down.ConnectorId);
        var upExit = exits.Single(exit => exit.ConnectorId == up.ConnectorId);
        Assert.Equal(hallway.Width / 2, downExit.Point.X);
        Assert.Equal(hallway.Width / 2, upExit.Point.X);
    }

    [Theory]
    [InlineData(StairDirection.Up)]
    [InlineData(StairDirection.Down)]
    public void ResolveExits_MarksAStairsExitWithItsDirection(StairDirection direction)
    {
        // Arrange
        var request = StairsRequest(lowerFloorNumber: 0, direction);

        // Act
        var exit = ExitFor(Frame, request);

        // Assert
        Assert.Equal(direction, exit.Stairs);
    }

    [Fact]
    public void ResolveExits_LeavesDoorsWithoutAStairDirection()
    {
        // Arrange
        var request = Request(ConnectorExitKind.SouthDoor);

        // Act
        var exit = ExitFor(Frame, request);

        // Assert
        Assert.Null(exit.Stairs);
    }

    [Fact]
    public void ResolveArrival_LandsPastTheFrontOfTheStairs_ForAStairsExit()
    {
        // Arrange
        var exit = new ConnectorExit(Guid.NewGuid(), new PlanarPoint(5, 0), Math.PI)
        {
            Stairs = StairDirection.Down,
        };

        // Act
        var arrival = ConnectorPointResolver.ResolveArrival(exit);

        // Assert
        Assert.True(arrival.Y >= StairPlan.Depth + 0.35);
        Assert.Equal(5, arrival.X, 6);
    }

    [Fact]
    public void ResolveArrival_LandsOneMeterAheadOfADoor()
    {
        // Arrange
        var exit = new ConnectorExit(Guid.NewGuid(), new PlanarPoint(5, 0), Math.PI);

        // Act
        var arrival = ConnectorPointResolver.ResolveArrival(exit);

        // Assert
        Assert.Equal(ConnectorPointResolver.ArrivalInset, arrival.Y, 6);
    }

    private static ConnectorExitRequest StairsRequest(
        int lowerFloorNumber,
        StairDirection direction
    ) =>
        new(Guid.NewGuid(), Guid.NewGuid(), ConnectorExitKind.Stairs)
        {
            LowerFloorNumber = lowerFloorNumber,
            Flight = direction,
        };

    [Fact]
    public void ResolveExits_PlacesHallwayDoorsAlongTheSideWallsByRoomDepth()
    {
        // Arrange
        var hallway = new Footprint(Width: 2.5, Depth: 24);
        var requests = Enumerable
            .Range(0, 2)
            .Select(index => new ConnectorExitRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ConnectorExitKind.HallwayDoor
            )
            {
                SideIndex = index,
                DestinationDepth = 3.5,
            })
            .ToArray();

        // Act
        var exits = ConnectorPointResolver.ResolveExits(hallway, requests);

        // Assert
        Assert.Equal(
            [new PlanarPoint(2.5, 12), new PlanarPoint(0, 12)],
            requests.Select(request =>
                exits.Single(exit => exit.ConnectorId == request.ConnectorId).Point
            )
        );
    }

    [Theory]
    [InlineData(0, 150, 0)]
    [InlineData(Math.PI, 150, 300)]
    [InlineData(Math.PI / 2, 300, 150)]
    [InlineData(3 * Math.PI / 2, 0, 150)]
    public void ResolveExits_MapsACardinalBearingToTheMatchingEdgeCenter(
        double bearing,
        double expectedX,
        double expectedY
    )
    {
        // Arrange
        var wilderness = LocationSizer.SizeWilderness();
        var request = Request(ConnectorExitKind.Bearing, bearingRadians: bearing);

        // Act
        var exit = ExitFor(wilderness, request);

        // Assert
        Assert.Equal(expectedX, exit.Point.X, precision: 6);
        Assert.Equal(expectedY, exit.Point.Y, precision: 6);
    }

    [Fact]
    public void ResolveExits_PlacesAnObliqueBearingOnTheEdgeItHitsFirst()
    {
        // Arrange
        var wilderness = LocationSizer.SizeWilderness();
        var request = Request(ConnectorExitKind.Bearing, bearingRadians: Math.PI / 3);

        // Act
        var exit = ExitFor(wilderness, request);

        // Assert
        Assert.Equal(300, exit.Point.X, precision: 6);
        Assert.Equal(150 - 0.5 * (150 / Math.Sin(Math.PI / 3)), exit.Point.Y, precision: 6);
    }

    [Fact]
    public void ResolveExits_SeparatesBearingExitsThatLandWithinSixMeters()
    {
        // Arrange
        var wilderness = LocationSizer.SizeWilderness();
        var requests = new[]
        {
            Request(ConnectorExitKind.Bearing, bearingRadians: 0.001),
            Request(ConnectorExitKind.Bearing, bearingRadians: 0.002),
            Request(ConnectorExitKind.Bearing, bearingRadians: 0.003),
        };

        // Act
        var exits = ConnectorPointResolver.ResolveExits(wilderness, requests);

        // Assert
        var positions = exits.Select(exit => exit.Point.X).Order().ToArray();
        Assert.True(positions[1] - positions[0] >= 6 - 1e-9);
        Assert.True(positions[2] - positions[1] >= 6 - 1e-9);
    }

    [Fact]
    public void ResolveExits_KeepsSeparatedBearingExitsInsideTheEdge_WhenTheyCrowdTheEnd()
    {
        // Arrange
        var wilderness = LocationSizer.SizeWilderness();
        var requests = Enumerable
            .Range(0, 3)
            .Select(_ => Request(ConnectorExitKind.Bearing, bearingRadians: Math.PI / 4 - 0.001))
            .ToArray();

        // Act
        var exits = ConnectorPointResolver.ResolveExits(wilderness, requests);

        // Assert
        Assert.All(exits, exit => Assert.InRange(exit.Point.X, 1, 299));
        var positions = exits.Select(exit => exit.Point.X).Order().ToArray();
        Assert.True(positions[1] - positions[0] >= 6 - 1e-9);
        Assert.True(positions[2] - positions[1] >= 6 - 1e-9);
    }

    [Fact]
    public void ResolveExits_PlacesAFixedExitAtItsGivenPoint()
    {
        // Arrange
        var request = new ConnectorExitRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ConnectorExitKind.Fixed
        )
        {
            FixedPoint = new PlanarPoint(12, 7),
            FixedFacingAngle = Math.PI,
        };

        // Act
        var exit = ExitFor(Frame, request);

        // Assert
        Assert.Equal(new PlanarPoint(12, 7), exit.Point);
        Assert.Equal(Math.PI, exit.FacingAngle);
    }

    [Fact]
    public void ResolveExits_ReturnsTheSameExits_ForTheSameRequests()
    {
        // Arrange
        var requests = Enumerable
            .Range(0, 5)
            .Select(index =>
                Request(
                    ConnectorExitKind.Compass,
                    destinationLocationId: new Guid(index, 0, 0, new byte[8])
                )
            )
            .ToArray();

        // Act
        var first = ConnectorPointResolver.ResolveExits(Frame, requests);
        var second = ConnectorPointResolver.ResolveExits(Frame, requests);

        // Assert
        Assert.Equal(first, second);
    }
}
