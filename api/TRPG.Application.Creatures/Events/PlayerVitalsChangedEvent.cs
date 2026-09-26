using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Results;
using TRPG.Domain;

namespace TRPG.Application.Creatures.Events;

public record PlayerVitalsChangedEvent(CreatureVitals Vitals, GameInstant GameTime)
    : GameClientEvent;
