using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Events;

public record WeatherChangedEvent(Guid WorldId, WeatherCondition? WeatherCondition)
    : GameClientEvent(WorldId);
