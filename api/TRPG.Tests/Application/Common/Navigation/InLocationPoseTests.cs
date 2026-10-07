using TRPG.Application.Common.Navigation;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Common.Navigation;

public class InLocationPoseTests
{
    private const double SettledAngle = 1.25;
    private const double Tolerance = 1e-9;

    private static readonly GameInstant EnteredAt = new(
        new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
    );

    [Fact]
    public void Resolve_ReturnsFirstPoint_WhenNowIsEntryTime()
    {
        // Arrange
        Point[] path = [new(2, 3), new(12, 3)];

        // Act
        var pose = InLocationPose.Resolve(path, EnteredAt, 2, EnteredAt, SettledAngle);

        // Assert
        Assert.Equal(2, pose.X, Tolerance);
        Assert.Equal(3, pose.Y, Tolerance);
    }

    [Fact]
    public void Resolve_ReturnsFirstPoint_WhenNowIsBeforeEntry()
    {
        // Arrange
        Point[] path = [new(2, 3), new(12, 3)];

        // Act
        var pose = InLocationPose.Resolve(
            path,
            EnteredAt,
            2,
            EnteredAt - TimeSpan.FromSeconds(10),
            SettledAngle
        );

        // Assert
        Assert.Equal(2, pose.X, Tolerance);
        Assert.Equal(3, pose.Y, Tolerance);
    }

    [Fact]
    public void Resolve_InterpolatesAlongSegment_WhenWalkIsPartway()
    {
        // Arrange
        Point[] path = [new(0, 0), new(10, 0)];

        // Act
        var pose = InLocationPose.Resolve(
            path,
            EnteredAt,
            2,
            EnteredAt + TimeSpan.FromSeconds(2.5),
            SettledAngle
        );

        // Assert
        Assert.Equal(5, pose.X, Tolerance);
        Assert.Equal(0, pose.Y, Tolerance);
    }

    [Fact]
    public void Resolve_ContinuesOntoNextSegment_WhenElapsedPassesACorner()
    {
        // Arrange
        Point[] path = [new(0, 0), new(4, 0), new(4, 6)];

        // Act
        var pose = InLocationPose.Resolve(
            path,
            EnteredAt,
            2,
            EnteredAt + TimeSpan.FromSeconds(4),
            SettledAngle
        );

        // Assert
        Assert.Equal(4, pose.X, Tolerance);
        Assert.Equal(4, pose.Y, Tolerance);
        Assert.Equal(Math.PI, pose.Angle, Tolerance);
    }

    [Fact]
    public void Resolve_ReturnsLastPointWithSettledAngle_WhenWalkIsFinished()
    {
        // Arrange
        Point[] path = [new(0, 0), new(4, 0), new(4, 6)];

        // Act
        var pose = InLocationPose.Resolve(
            path,
            EnteredAt,
            2,
            EnteredAt + TimeSpan.FromHours(1),
            SettledAngle
        );

        // Assert
        Assert.Equal(new Placement(4, 6, SettledAngle), pose);
    }

    [Fact]
    public void Resolve_ReturnsSettledPose_WhenPathIsASinglePoint()
    {
        // Arrange
        Point[] path = [new(7, 8)];

        // Act
        var pose = InLocationPose.Resolve(path, EnteredAt, 2, EnteredAt, SettledAngle);

        // Assert
        Assert.Equal(new Placement(7, 8, SettledAngle), pose);
    }

    [Fact]
    public void Resolve_SkipsZeroLengthSegments_WhenPathRepeatsAPoint()
    {
        // Arrange
        Point[] path = [new(0, 0), new(0, 0), new(6, 0)];

        // Act
        var pose = InLocationPose.Resolve(
            path,
            EnteredAt,
            2,
            EnteredAt + TimeSpan.FromSeconds(1),
            SettledAngle
        );

        // Assert
        Assert.Equal(2, pose.X, Tolerance);
    }

    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, 0, Math.PI / 2)]
    [InlineData(0, 1, Math.PI)]
    [InlineData(-1, 0, 3 * Math.PI / 2)]
    public void Resolve_FacesTheDirectionOfTravel_WhenWalking(double x, double y, double angle)
    {
        // Arrange
        Point[] path = [new(0, 0), new(x * 10, y * 10)];

        // Act
        var pose = InLocationPose.Resolve(path, EnteredAt, 1, EnteredAt, SettledAngle);

        // Assert
        Assert.Equal(angle, pose.Angle, Tolerance);
    }

    [Fact]
    public void Resolve_Throws_WhenPathIsEmpty()
    {
        // Arrange
        Point[] path = [];

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            InLocationPose.Resolve(path, EnteredAt, 2, EnteredAt, SettledAngle)
        );
    }

    [Fact]
    public void Resolve_Throws_WhenSpeedIsNotPositive()
    {
        // Arrange
        Point[] path = [new(0, 0), new(1, 0)];

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InLocationPose.Resolve(path, EnteredAt, 0, EnteredAt, SettledAngle)
        );
    }
}
