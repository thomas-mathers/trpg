using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Events;

public record WeatherChangedEvent(
    Guid WorldId,
    Guid LocationId,
    WorldStateStamp Stamp,
    WeatherCondition? WeatherCondition
) : GameClientEvent(WorldId);
