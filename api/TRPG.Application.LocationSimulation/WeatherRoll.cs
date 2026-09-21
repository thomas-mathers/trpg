using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation;

internal static class WeatherRoll
{
    private static readonly IReadOnlyDictionary<
        WeatherCondition,
        IReadOnlyDictionary<WeatherCondition, double>
    > BaseTransitionWeights = new Dictionary<
        WeatherCondition,
        IReadOnlyDictionary<WeatherCondition, double>
    >
    {
        [WeatherCondition.Clear] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 55,
            [WeatherCondition.Cloudy] = 30,
            [WeatherCondition.Rain] = 5,
            [WeatherCondition.Storm] = 2,
            [WeatherCondition.Snow] = 3,
            [WeatherCondition.Fog] = 5,
        },
        [WeatherCondition.Cloudy] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 25,
            [WeatherCondition.Cloudy] = 40,
            [WeatherCondition.Rain] = 20,
            [WeatherCondition.Storm] = 5,
            [WeatherCondition.Snow] = 5,
            [WeatherCondition.Fog] = 5,
        },
        [WeatherCondition.Rain] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 10,
            [WeatherCondition.Cloudy] = 25,
            [WeatherCondition.Rain] = 45,
            [WeatherCondition.Storm] = 15,
            [WeatherCondition.Snow] = 0,
            [WeatherCondition.Fog] = 5,
        },
        [WeatherCondition.Storm] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 5,
            [WeatherCondition.Cloudy] = 15,
            [WeatherCondition.Rain] = 30,
            [WeatherCondition.Storm] = 45,
            [WeatherCondition.Snow] = 0,
            [WeatherCondition.Fog] = 5,
        },
        [WeatherCondition.Snow] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 10,
            [WeatherCondition.Cloudy] = 20,
            [WeatherCondition.Rain] = 0,
            [WeatherCondition.Storm] = 5,
            [WeatherCondition.Snow] = 60,
            [WeatherCondition.Fog] = 5,
        },
        [WeatherCondition.Fog] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 20,
            [WeatherCondition.Cloudy] = 30,
            [WeatherCondition.Rain] = 10,
            [WeatherCondition.Storm] = 0,
            [WeatherCondition.Snow] = 5,
            [WeatherCondition.Fog] = 35,
        },
    };

    private static readonly IReadOnlyDictionary<
        Season,
        IReadOnlyDictionary<WeatherCondition, double>
    > SeasonMultipliers = new Dictionary<Season, IReadOnlyDictionary<WeatherCondition, double>>
    {
        [Season.Winter] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 0.7,
            [WeatherCondition.Cloudy] = 1.0,
            [WeatherCondition.Rain] = 0.3,
            [WeatherCondition.Storm] = 1.0,
            [WeatherCondition.Snow] = 3.0,
            [WeatherCondition.Fog] = 1.2,
        },
        [Season.Spring] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 1.0,
            [WeatherCondition.Cloudy] = 1.2,
            [WeatherCondition.Rain] = 1.5,
            [WeatherCondition.Storm] = 1.2,
            [WeatherCondition.Snow] = 0.3,
            [WeatherCondition.Fog] = 1.0,
        },
        [Season.Summer] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 1.6,
            [WeatherCondition.Cloudy] = 0.8,
            [WeatherCondition.Rain] = 0.6,
            [WeatherCondition.Storm] = 1.0,
            [WeatherCondition.Snow] = 0.0,
            [WeatherCondition.Fog] = 0.4,
        },
        [Season.Autumn] = new Dictionary<WeatherCondition, double>
        {
            [WeatherCondition.Clear] = 0.9,
            [WeatherCondition.Cloudy] = 1.3,
            [WeatherCondition.Rain] = 1.2,
            [WeatherCondition.Storm] = 1.1,
            [WeatherCondition.Snow] = 0.4,
            [WeatherCondition.Fog] = 1.3,
        },
    };

    public static WeatherCondition InitialCondition(Season season)
    {
        var seasonWeights = SeasonMultipliers[season];
        var options = seasonWeights.Select(pair => (pair.Key, Weight: pair.Value)).ToArray();
        return SampleWeighted(options);
    }

    public static WeatherCondition NextCondition(WeatherCondition current, Season season)
    {
        var seasonWeights = SeasonMultipliers[season];
        var options = BaseTransitionWeights[current]
            .Select(pair => (pair.Key, Weight: pair.Value * seasonWeights[pair.Key]))
            .ToArray();
        return SampleWeighted(options);
    }

    public static TimeSpan NextChangeOffset() =>
        Random.Shared.Next(4, 17) * GameClock.RealTimePerInGameHour;

    private static WeatherCondition SampleWeighted(
        IReadOnlyCollection<(WeatherCondition Condition, double Weight)> options
    )
    {
        var totalWeight = options.Sum(option => option.Weight);
        var roll = Random.Shared.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var option in options)
        {
            cumulative += option.Weight;
            if (roll < cumulative)
            {
                return option.Condition;
            }
        }

        return options.Last().Condition;
    }
}
