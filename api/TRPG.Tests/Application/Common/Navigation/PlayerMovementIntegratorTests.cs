using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Common.Navigation;

public class PlayerMovementIntegratorTests
{
    private static readonly MovementSpace OpenField = new(20, 20, []);
    private static readonly MovementObstacle Crate = new(10, 10, 0, 4, 2);

    [Fact]
    public void Advance_WalksNorth_WhenFacingNorth()
    {
        // Act
        var position = Advance(new Point(5, 5), new MovementInput(1, 0, 0));

        // Assert
        AssertAt(5, 3, position);
    }

    [Fact]
    public void Advance_WalksEast_WhenFacingEast()
    {
        // Act
        var position = Advance(new Point(5, 5), new MovementInput(1, 0, Math.PI / 2));

        // Assert
        AssertAt(7, 5, position);
    }

    [Fact]
    public void Advance_StrafesRightOfTheHeading()
    {
        // Act
        var position = Advance(new Point(5, 5), new MovementInput(0, 1, 0));

        // Assert
        AssertAt(7, 5, position);
    }

    [Fact]
    public void Advance_WalksBackwardsOppositeTheHeading()
    {
        // Act
        var position = Advance(new Point(5, 5), new MovementInput(-1, 0, 0));

        // Assert
        AssertAt(5, 7, position);
    }

    [Fact]
    public void Advance_StaysPut_WhenThereIsNoInput()
    {
        // Act
        var position = Advance(new Point(5, 5), new MovementInput(0, 0, 1));

        // Assert
        AssertAt(5, 5, position);
    }

    [Fact]
    public void Advance_StopsOneRadiusShortOfAFace_WhenWalkingIntoAnObstacle()
    {
        // Arrange
        var space = new MovementSpace(20, 20, [Crate]);

        // Act
        var position = Advance(new Point(10, 5), new MovementInput(1, 0, Math.PI), space, 3);

        // Assert
        AssertAt(10, 9 - PlayerMovementIntegrator.PlayerRadius, position);
    }

    [Fact]
    public void Advance_RespectsTheObstacleRotation()
    {
        // Arrange
        var rotated = Crate with
        {
            Angle = Math.PI / 2,
        };
        var space = new MovementSpace(20, 20, [rotated]);

        // Act
        var position = Advance(new Point(10, 15), new MovementInput(1, 0, 0), space, 3);

        // Assert
        AssertAt(10, 12 + PlayerMovementIntegrator.PlayerRadius, position);
    }

    [Fact]
    public void Advance_EjectsThroughTheNearestFace_WhenStartingInsideAnObstacle()
    {
        // Arrange
        var space = new MovementSpace(20, 20, [Crate]);

        // Act
        var position = Advance(new Point(10, 9.5), new MovementInput(0, 0, 0), space, 0.01);

        // Assert
        AssertAt(10, 9 - PlayerMovementIntegrator.PlayerRadius, position);
    }

    [Fact]
    public void Advance_RoundsACorner_ByKeepingTheRadiusFromTheCornerPoint()
    {
        // Arrange
        var space = new MovementSpace(20, 20, [Crate]);

        // Act
        var position = Advance(new Point(13, 8), new MovementInput(-0.5, -0.5, 0), space, 0.8);

        // Assert
        var distance = Math.Sqrt(Math.Pow(position.X - 12, 2) + Math.Pow(position.Y - 9, 2));
        Assert.Equal(PlayerMovementIntegrator.PlayerRadius, distance, 5);
    }

    [Fact]
    public void Advance_StopsAtTheBoundsMargin_WhenWalkingOffTheEdge()
    {
        // Act
        var position = Advance(new Point(1, 5), new MovementInput(0, -1, 0), speed: 5, seconds: 3);

        // Assert
        AssertAt(0.3, 5, position);
    }

    [Fact]
    public void Advance_CentresOnALocationNarrowerThanTheMargin()
    {
        // Arrange
        var space = new MovementSpace(0.2, 0.2, []);

        // Act
        var position = Advance(new Point(3, 3), new MovementInput(0, 0, 0), space, 0.01);

        // Assert
        AssertAt(0.1, 0.1, position);
    }

    [Fact]
    public void Advance_DoesNotTunnelThroughAThinWall_WhenTheElapsedTimeIsLong()
    {
        // Arrange
        var wall = new MovementObstacle(10, 5, 0, 20, 0.2);
        var space = new MovementSpace(20, 20, [wall]);

        // Act
        var position = Advance(new Point(10, 15), new MovementInput(1, 0, 0), space, 3, speed: 6);

        // Assert
        AssertAt(10, 5.1 + PlayerMovementIntegrator.PlayerRadius, position);
    }

    private static Point Advance(Point start, MovementInput input, double speed, double seconds) =>
        Advance(start, input, OpenField, seconds, speed);

    private static Point Advance(Point start, MovementInput input) =>
        Advance(start, input, OpenField, 1, 2);

    private static Point Advance(
        Point start,
        MovementInput input,
        MovementSpace space,
        double seconds,
        double speed = 2
    ) =>
        PlayerMovementIntegrator.Advance(start, input, speed, TimeSpan.FromSeconds(seconds), space);

    private static void AssertAt(double x, double y, Point position)
    {
        Assert.Equal(x, position.X, 5);
        Assert.Equal(y, position.Y, 5);
    }
}
