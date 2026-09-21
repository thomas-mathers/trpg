using TRPG.Application.LocationSimulation;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.LocationSimulation;

public sealed class WeatherRollTests
{
    [Fact]
    public void InitialCondition_NeverReturnsSnow_InSummer()
    {
        // Arrange - Summer's Snow multiplier is zero, so it must be unreachable regardless of roll
        var results = Enumerable
            .Range(0, 200)
            .Select(_ => WeatherRoll.InitialCondition(Season.Summer));

        // Act & Assert
        Assert.DoesNotContain(WeatherCondition.Snow, results);
    }

    [Fact]
    public void NextCondition_NeverReturnsSnow_InSummer()
    {
        // Arrange - even starting from Snow itself, Summer's zero multiplier rules it out
        var results = Enumerable
            .Range(0, 200)
            .Select(_ => WeatherRoll.NextCondition(WeatherCondition.Snow, Season.Summer));

        // Act & Assert
        Assert.DoesNotContain(WeatherCondition.Snow, results);
    }

    [Fact]
    public void NextChangeOffset_StaysWithinTheConfiguredInGameHourBounds()
    {
        // Arrange
        var offsets = Enumerable.Range(0, 200).Select(_ => WeatherRoll.NextChangeOffset());

        // Act & Assert
        Assert.All(
            offsets,
            offset =>
            {
                Assert.True(offset >= 4 * GameClock.RealTimePerInGameHour);
                Assert.True(offset <= 16 * GameClock.RealTimePerInGameHour);
            }
        );
    }
}
