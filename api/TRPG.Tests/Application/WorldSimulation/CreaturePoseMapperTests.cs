using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldSimulation;

public class CreaturePoseMapperTests
{
    [Fact]
    public void Map_PersistsTheLocationEntryEndpoint()
    {
        // Arrange
        var endpoint = new Point(4, 7);
        var arrival = new LocationEntered(
            Guid.NewGuid(),
            GameClock.Epoch,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            endpoint,
            Guid.NewGuid()
        );

        // Act
        var update = new CreaturePoseMapper().MapEvent(arrival);

        // Assert
        Assert.Equal(endpoint, update.StandAt);
    }

    [Fact]
    public void Map_PersistsTheJourneyEndpoint()
    {
        // Arrange
        var endpoint = new Point(4, 7);
        var completed = new JourneyCompleted(
            Guid.NewGuid(),
            GameClock.Epoch,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatureJobAction.Work
        )
        {
            StopPosition = endpoint,
        };

        // Act
        var update = new CreaturePoseMapper().MapEvent(completed);

        // Assert
        Assert.Equal(endpoint, update.StandAt);
    }

    [Fact]
    public void Map_FacesAPursuitingCreatureAlongItsLastSegment_WhenThePursuitCompletes()
    {
        // Arrange
        var path = new[] { new Point(0, 0), new Point(0, -5), new Point(5, -5) };
        var move = new LocalMovePlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            CreatureJobAction.Idle,
            LocalMoveTargetKind.Pursuit,
            Guid.Empty,
            path
        );
        var completed = new LocalMoveCompleted(
            move.CreatureId,
            GameClock.Epoch,
            move.LocationId,
            move,
            path[^1]
        );

        // Act
        var update = new CreaturePoseMapper().MapEvent(completed);

        // Assert
        Assert.Equal(Math.PI / 2, update.StandAngle!.Value, 6);
    }
}
