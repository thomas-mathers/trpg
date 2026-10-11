using TRPG.Application.Configuration;

namespace TRPG.Tests.Application.Configuration;

public sealed class ContinuousTimeBalanceTests
{
    [Fact]
    public void ActionPointsRegeneration_RefillsFromEmptyInFiftySeconds_WhenDefault()
    {
        // Arrange
        var options = new CreatureRegenOptions();

        // Act
        var refill = options.TickInterval * (1 / options.ApRegenPercentPerTick);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(50), refill);
    }

    [Fact]
    public void HitPointsRegeneration_RefillsFromEmptyInOneHundredSeconds_WhenDefault()
    {
        // Arrange
        var options = new CreatureRegenOptions();

        // Act
        var refill = options.TickInterval * (1 / options.HpRegenPercentPerTick);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(100), refill);
    }
}
