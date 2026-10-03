using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Results;

namespace TRPG.Application.Creatures.Events;

public record PlayerVitalsChangedEvent(Guid WorldId, CreatureVitals Vitals, long Version)
    : GameClientEvent(WorldId);
