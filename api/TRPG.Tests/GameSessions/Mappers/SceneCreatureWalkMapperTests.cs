using TRPG.Application.Scenes.Results;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.GameSessions.Mappers;
using TRPG.GameSessions.Responses;

namespace TRPG.Tests.GameSessions.Mappers;

public sealed class SceneCreatureWalkMapperTests
{
    [Fact]
    public void ToWire_ExpressesTheStartAsMillisecondsSinceTheGameEpoch()
    {
        // Arrange
        var walk = new SceneCreatureWalk(
            [new Point(1, 2), new Point(3, 4)],
            GameClock.Epoch + TimeSpan.FromSeconds(90),
            1.5,
            true
        );

        // Act
        var wire = walk.ToWire();

        // Assert
        Assert.Equal(90_000, wire.StartedAtGameTimeMilliseconds);
    }

    [Fact]
    public void ToWire_KeepsThePathInOrder()
    {
        // Arrange
        var walk = new SceneCreatureWalk(
            [new Point(1, 2), new Point(3, 4)],
            GameClock.Epoch,
            1.5,
            false
        );

        // Act
        var wire = walk.ToWire();

        // Assert
        Assert.Equal([new PointWire(1, 2), new PointWire(3, 4)], wire.Points);
    }

    [Fact]
    public void ToWire_ExpressesThePauseAsMillisecondsSinceTheGameEpoch()
    {
        // Arrange
        var walk = new SceneCreatureWalk(
            [new Point(1, 2), new Point(3, 4)],
            GameClock.Epoch,
            1.5,
            false,
            GameClock.Epoch + TimeSpan.FromSeconds(7)
        );

        // Act
        var wire = walk.ToWire();

        // Assert
        Assert.Equal(7_000, wire.PausedAtGameTimeMilliseconds);
    }
}
