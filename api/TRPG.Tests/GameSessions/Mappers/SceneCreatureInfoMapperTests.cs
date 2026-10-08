using TRPG.GameSessions.Mappers;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.GameSessions.Mappers;

public sealed class SceneCreatureInfoMapperTests
{
    [Theory]
    [InlineData(50, 1.68f)]
    [InlineData(25, 0.84f)]
    public void ToStatusSnapshot_ExposesTheWalkPaceInMetersPerSecond_ForTheMovementSpeed(
        float movementSpeed,
        float expected
    )
    {
        // Arrange
        var creature = SceneResultBuilder.MakeCreature("Hero") with
        {
            MovementSpeed = movementSpeed,
        };

        // Act
        var snapshot = creature.ToStatusSnapshot();

        // Assert
        Assert.Equal(expected, snapshot.WalkMetersPerSecond, 0.001f);
    }
}
