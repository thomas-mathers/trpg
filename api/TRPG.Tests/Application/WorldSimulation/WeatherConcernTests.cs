using TRPG.Application.Weather.Results;
using TRPG.Application.WorldSimulation.Climate;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class WeatherConcernTests
{
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 12, 0, 0));

    [Fact]
    public void Tick_EmitsNothing_WhenNoForecastIsDue()
    {
        // Arrange
        var forecast = new WeatherForecast(
            Guid.NewGuid(),
            WeatherCondition.Clear,
            Now + TimeSpan.FromMinutes(1)
        );

        // Act
        var events = WeatherConcern.Tick([forecast], Now);

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void Tick_SchedulesTheNextChangeAfterNow_WhenAForecastIsDue()
    {
        // Arrange
        var forecast = new WeatherForecast(Guid.NewGuid(), WeatherCondition.Clear, Now);

        // Act
        var events = WeatherConcern.Tick([forecast], Now);

        // Assert
        var changed = Assert.IsType<WeatherChanged>(Assert.Single(events));
        Assert.Equal(
            (forecast.StateId, Now, true),
            (changed.Forecast.StateId, changed.At, changed.Forecast.NextChangeAt > Now)
        );
    }

    [Fact]
    public void Tick_EmitsOnlyTheDueForecasts_WhenSomeAreNotDue()
    {
        // Arrange
        var due = new WeatherForecast(Guid.NewGuid(), WeatherCondition.Rain, Now);
        var notDue = new WeatherForecast(
            Guid.NewGuid(),
            WeatherCondition.Fog,
            Now + TimeSpan.FromHours(2)
        );

        // Act
        var events = WeatherConcern.Tick([due, notDue], Now);

        // Assert
        var changed = Assert.IsType<WeatherChanged>(Assert.Single(events));
        Assert.Equal(due.StateId, changed.Forecast.StateId);
    }
}
