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
}
