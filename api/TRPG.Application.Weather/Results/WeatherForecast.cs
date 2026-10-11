using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Weather.Results;

public sealed record WeatherForecast(
    Guid StateId,
    WeatherCondition Condition,
    GameInstant NextChangeAt
);
