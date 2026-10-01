using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CreaturePlacementResolverTests
{
    private static readonly Footprint Frame = new(Width: 8, Depth: 6);

    [Fact]
    public void PlaceAt_ReturnsThePreferredPose_WhenTheSpotIsFree()
    {
        // Arrange
        var preferred = new Placement(4, 5, Math.PI);

        // Act
        var placement = CreaturePlacementResolver.PlaceAt(Frame, [], preferred);

        // Assert
        Assert.Equal(preferred, placement);
    }

    [Fact]
    public void PlaceAt_MovesToANearbyFreeSpotKeepingTheFacing_WhenThePreferredSpotIsBlocked()
    {
        // Arrange
        var preferred = new Placement(4, 5, Math.PI);
        var blocker = Obstacle(4, 5, 1, 1);

        // Act
        var placement = CreaturePlacementResolver.PlaceAt(Frame, [blocker], preferred);

        // Assert
        Assert.True(Distance(placement, preferred) <= 1.5);
        Assert.Equal(preferred.Angle, placement.Angle);
        Assert.False(Overlaps(placement, blocker));
    }

    [Fact]
    public void PlaceNear_StaysCloseToTheAnchorAndFacesIt()
    {
        // Arrange
        var anchor = Obstacle(4, 3, 1, 1);

        // Act
        var placement = CreaturePlacementResolver.PlaceNear(
            Frame,
            [anchor],
            anchor.Placement,
            seed: 1
        );

        // Assert
        Assert.True(Distance(placement, anchor.Placement) <= 1.5);
        Assert.True(FacingAlignment(placement, anchor.Placement) > 0.99);
    }

    [Fact]
    public void PlaceNear_AvoidsEveryObstacle()
    {
        // Arrange
        var anchor = Obstacle(4, 3, 1, 1);
        PlacementObstacle[] obstacles =
        [
            anchor,
            Obstacle(3, 3, 1, 1),
            Obstacle(5, 3, 1, 1),
            Obstacle(4, 2, 1, 1),
        ];

        // Act
        var placement = CreaturePlacementResolver.PlaceNear(
            Frame,
            obstacles,
            anchor.Placement,
            seed: 1
        );

        // Assert
        Assert.All(obstacles, obstacle => Assert.False(Overlaps(placement, obstacle)));
    }

    [Fact]
    public void PlaceFree_KeepsTheCreatureInsideTheFrameAndClearOfObstacles()
    {
        // Arrange
        PlacementObstacle[] obstacles = [Obstacle(2, 2, 3, 2), Obstacle(6, 4, 2, 2)];

        // Act
        var placement = CreaturePlacementResolver.PlaceFree(Frame, obstacles, seed: 7);

        // Assert
        Assert.True(IsInside(placement));
        Assert.All(obstacles, obstacle => Assert.False(Overlaps(placement, obstacle)));
    }

    [Fact]
    public void PlaceFree_ReturnsTheSamePose_WhenTheSeedIsTheSame()
    {
        // Arrange
        PlacementObstacle[] obstacles = [Obstacle(2, 2, 3, 2)];
        var first = CreaturePlacementResolver.PlaceFree(Frame, obstacles, seed: 42);

        // Act
        var second = CreaturePlacementResolver.PlaceFree(Frame, obstacles, seed: 42);

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void PlaceFree_ReturnsDifferentPoses_WhenTheSeedsDiffer()
    {
        // Arrange
        var poses = Enumerable
            .Range(1, 10)
            .Select(seed => CreaturePlacementResolver.PlaceFree(Frame, [], seed))
            .ToArray();

        // Act
        var distinct = poses.Distinct().Count();

        // Assert
        Assert.True(distinct > 1);
    }

    [Fact]
    public void PlaceFree_StaysInsideTheFrame_WhenNoSpotIsFree()
    {
        // Arrange
        var frame = new Footprint(Width: 12, Depth: 12);
        PlacementObstacle[] obstacles = [Obstacle(6, 6, 11.4, 11.4), Obstacle(6, 6, 0.2, 0.2)];

        // Act
        var placement = CreaturePlacementResolver.PlaceFree(frame, obstacles, seed: 3);

        // Assert
        Assert.True(IsInside(placement, frame));
    }

    [Fact]
    public void PlaceAt_StaysInsideTheFrame_WhenTheRoomIsCompletelyBlocked()
    {
        // Arrange
        var frame = new Footprint(Width: 3, Depth: 3);
        var preferred = new Placement(2.9, 0.1, 0);

        // Act
        var placement = CreaturePlacementResolver.PlaceAt(
            frame,
            [Obstacle(1.5, 1.5, 3, 3)],
            preferred
        );

        // Assert
        Assert.True(IsInside(placement, frame));
    }

    private static PlacementObstacle Obstacle(double x, double y, double width, double depth) =>
        new(new Placement(x, y, 0), new Footprint(width, depth));

    private static double Distance(Placement first, Placement second) =>
        Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));

    private static double FacingAlignment(Placement from, Placement target)
    {
        var length = Distance(from, target);
        var towardX = (target.X - from.X) / length;
        var towardY = (target.Y - from.Y) / length;

        return (Math.Sin(from.Angle) * towardX) + (-Math.Cos(from.Angle) * towardY);
    }

    private static bool Overlaps(Placement creature, PlacementObstacle obstacle) =>
        OrientedBox
            .From(creature, CreaturePlacementResolver.Body)
            .Overlaps(OrientedBox.From(obstacle.Placement, obstacle.Footprint));

    private static bool IsInside(Placement creature) => IsInside(creature, Frame);

    private static bool IsInside(Placement creature, Footprint frame) =>
        OrientedBox
            .From(creature, CreaturePlacementResolver.Body)
            .IsInside(frame.Width, frame.Depth);
}
