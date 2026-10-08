using TRPG.Application.Common.Navigation;
using TRPG.Application.Creatures.Mappers;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Scenes.Navigation;
using TRPG.Application.Scenes.Results;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Navigation;

public class CreaturePoseResolverTests
{
    private const double Tolerance = 1e-6;

    private const double Pace = InLocationPace.WalkMetersPerSecond;

    private static readonly Point[] StraightWalk = [new(0, 0), new(30, 0)];

    private static readonly TimeSpan HalfWalk = TimeSpan.FromSeconds(15 / Pace);
    private static readonly TimeSpan FinishedWalk = TimeSpan.FromSeconds(30 / Pace + 1);

    private static readonly GameInstant StartedAt = new(
        new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
    );

    [Fact]
    public void Resolve_ReturnsTheAnchor_WhenTheCreatureHasNoWalk()
    {
        // Arrange
        var creature = MakeResult(x: 10, y: 4, angle: 1.5);

        // Act
        var pose = CreaturePoseResolver.Resolve(creature, [], StartedAt, timeScale: 1);

        // Assert
        Assert.Equal(new Placement(10, 4, 1.5), pose);
    }

    [Fact]
    public void Resolve_ReturnsAPointAlongTheWalk_WhenTheWalkIsUnderway()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 1.5, entry: new Point(0, 0));

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            StraightWalk,
            StartedAt + HalfWalk,
            timeScale: 1
        );

        // Assert
        Assert.Equal(15, pose.X, Tolerance);
    }

    [Fact]
    public void Resolve_ReturnsTheAnchorWithItsAngle_WhenTheWalkIsFinished()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 1.5, entry: new Point(0, 0));

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            StraightWalk,
            StartedAt + TimeSpan.FromHours(1),
            timeScale: 1
        );

        // Assert
        Assert.Equal(new Placement(30, 0, 1.5), pose);
    }

    [Fact]
    public void Resolve_WalksSlowerPerGameSecond_WhenTimePassesFasterThanReal()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 1.5, entry: new Point(0, 0));

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            StraightWalk,
            StartedAt + HalfWalk * 10,
            timeScale: 10
        );

        // Assert
        Assert.Equal(15, pose.X, Tolerance);
    }

    [Fact]
    public void Resolve_ReturnsAPointAlongTheExitWalk_WhenTheNpcIsLeaving()
    {
        // Arrange
        var creature = MakeResult(x: 0, y: 0, angle: 0, exit: new Point(30, 0));

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            StraightWalk,
            StartedAt + HalfWalk,
            timeScale: 1
        );

        // Assert
        Assert.Equal(15, pose.X, Tolerance);
    }

    [Fact]
    public void Resolve_IgnoresTheAnchor_WhenTheNpcIsPassingThrough()
    {
        // Arrange
        var creature = MakeResult(
            x: 99,
            y: 99,
            angle: 0,
            entry: new Point(0, 0),
            exit: new Point(30, 0)
        );

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            StraightWalk,
            StartedAt + HalfWalk,
            timeScale: 1
        );

        // Assert
        Assert.Equal(15, pose.X, Tolerance);
        Assert.Equal((0, Math.PI / 2), (pose.Y, pose.Angle));
    }

    [Fact]
    public void Resolve_HoldsThePausePose_WhenTheWalkIsPaused()
    {
        // Arrange
        var creature = MakeResult(
            x: 30,
            y: 0,
            angle: 1.5,
            entry: new Point(0, 0),
            walkPausedAt: StartedAt + HalfWalk
        );

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            StraightWalk,
            StartedAt + TimeSpan.FromSeconds(60),
            timeScale: 1
        );

        // Assert
        Assert.Equal(15, pose.X, Tolerance);
    }

    [Fact]
    public void HasLeft_ReturnsFalse_WhenAnExitWalkIsPausedBeforeItFinishes()
    {
        // Arrange
        var creature = MakeResult(
            x: 0,
            y: 0,
            angle: 0,
            exit: new Point(30, 0),
            walkPausedAt: StartedAt + HalfWalk
        );

        // Act
        var hasLeft = CreaturePoseResolver.HasLeft(
            creature,
            StraightWalk,
            StartedAt + TimeSpan.FromHours(1),
            timeScale: 1
        );

        // Assert
        Assert.False(hasLeft);
    }

    [Fact]
    public void BuildWalk_CarriesThePauseInstant_WhenTheWalkIsPaused()
    {
        // Arrange
        var pausedAt = StartedAt + HalfWalk;
        var creature = MakeResult(
            x: 30,
            y: 0,
            angle: 0,
            entry: new Point(0, 0),
            walkPausedAt: pausedAt
        );

        // Act
        var walk = CreaturePoseResolver.BuildWalk(creature, StraightWalk, timeScale: 1);

        // Assert
        Assert.Equal(pausedAt, walk!.PausedAt);
    }

    [Fact]
    public void HasLeft_ReturnsFalse_WhenTheExitWalkIsUnderway()
    {
        // Arrange
        var creature = MakeResult(x: 0, y: 0, angle: 0, exit: new Point(30, 0));

        // Act
        var hasLeft = CreaturePoseResolver.HasLeft(
            creature,
            StraightWalk,
            StartedAt + HalfWalk,
            timeScale: 1
        );

        // Assert
        Assert.False(hasLeft);
    }

    [Fact]
    public void HasLeft_ReturnsTrue_WhenTheExitWalkIsFinished()
    {
        // Arrange
        var creature = MakeResult(x: 0, y: 0, angle: 0, exit: new Point(30, 0));

        // Act
        var hasLeft = CreaturePoseResolver.HasLeft(
            creature,
            StraightWalk,
            StartedAt + FinishedWalk,
            timeScale: 1
        );

        // Assert
        Assert.True(hasLeft);
    }

    [Fact]
    public void HasLeft_ReturnsFalse_WhenOnlyAnEntryWalkIsFinished()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 0, entry: new Point(0, 0));

        // Act
        var hasLeft = CreaturePoseResolver.HasLeft(
            creature,
            StraightWalk,
            StartedAt + TimeSpan.FromHours(1),
            timeScale: 1
        );

        // Assert
        Assert.False(hasLeft);
    }

    [Fact]
    public void BuildWalk_ReturnsNull_WhenTheCreatureHasNoWalk()
    {
        // Arrange
        var creature = MakeResult(x: 10, y: 4, angle: 1.5);

        // Act
        var walk = CreaturePoseResolver.BuildWalk(creature, StraightWalk, timeScale: 1);

        // Assert
        Assert.Null(walk);
    }

    [Fact]
    public void BuildWalk_ReturnsNull_WhenThePathIsASinglePoint()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 0, entry: new Point(0, 0));

        // Act
        var walk = CreaturePoseResolver.BuildWalk(creature, [new Point(0, 0)], timeScale: 1);

        // Assert
        Assert.Null(walk);
    }

    [Fact]
    public void BuildWalk_StartsAtTheEntryInstantAndStays_WhenOnlyAnEntryWalkIsRecorded()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 0, entry: new Point(0, 0));

        // Act
        var walk = CreaturePoseResolver.BuildWalk(creature, StraightWalk, timeScale: 1);

        // Assert
        Assert.Equal(new SceneCreatureWalk(StraightWalk, StartedAt, Pace, false), walk);
    }

    [Fact]
    public void BuildWalk_LeavesAtTheEnd_WhenAnExitWalkIsRecorded()
    {
        // Arrange
        var creature = MakeResult(x: 0, y: 0, angle: 0, exit: new Point(30, 0));

        // Act
        var walk = CreaturePoseResolver.BuildWalk(creature, StraightWalk, timeScale: 1);

        // Assert
        Assert.True(walk!.LeavesAtEnd);
    }

    [Fact]
    public void BuildWalk_SlowsThePacePerGameSecond_WhenTimePassesFasterThanReal()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 0, entry: new Point(0, 0));

        // Act
        var walk = CreaturePoseResolver.BuildWalk(creature, StraightWalk, timeScale: 10);

        // Assert
        Assert.Equal(Pace / 10, walk!.MetersPerGameSecond, Tolerance);
    }

    private static CreatureResult MakeResult(
        double x,
        double y,
        double angle,
        Point? entry = null,
        Point? exit = null,
        GameInstant? walkPausedAt = null
    )
    {
        var creature = Builders.MakeCreature(
            x: x,
            y: y,
            angle: angle,
            movementSpeed: 50,
            entry: entry,
            enteredAt: entry == null ? null : StartedAt,
            exit: exit,
            departedAt: exit == null ? null : StartedAt,
            walkPausedAt: walkPausedAt
        );

        return creature.ToResult(
            gold: 0,
            stateId: Guid.NewGuid(),
            cityId: null,
            districtId: null,
            roomId: null
        );
    }
}
