using TRPG.Application.Configuration;

namespace TRPG.Tests.Application.Configuration;

public sealed class ContinuousTimeBalanceTests
{
    private static TimeSpan WalkingTime(float distance) =>
        TimeSpan.FromHours(distance / CreatureGeneratorOptions.WalkingSpeedUnitsPerHour);

    [Fact]
    public void BuildingDistance_TakesSixMinutesToWalk_WhenAtWalkingPace()
    {
        // Arrange
        var options = new CityTravelOptions();

        // Act
        var duration = WalkingTime(options.BuildingDistance);

        // Assert
        Assert.Equal(6, duration.TotalMinutes, precision: 3);
    }

    [Fact]
    public void DistrictDistance_TakesEighteenMinutesToWalk_WhenAtWalkingPace()
    {
        // Arrange
        var options = new CityTravelOptions();

        // Act
        var duration = WalkingTime(options.DistrictDistance);

        // Assert
        Assert.Equal(18, duration.TotalMinutes, precision: 3);
    }

    [Fact]
    public void CaravanSpeed_IsThreeTimesWalkingPace_WhenDefault()
    {
        // Act
        var options = new CaravanOptions();

        // Assert
        Assert.Equal(
            CreatureGeneratorOptions.WalkingSpeedUnitsPerHour * 3,
            options.SpeedUnitsPerHour
        );
    }

    [Fact]
    public void CaravanLinger_IsTwentyRealMinutes_WhenDefault()
    {
        // Act
        var options = new CaravanOptions();

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(20), TimeSpan.FromHours(options.DefaultLingerHours));
    }

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
