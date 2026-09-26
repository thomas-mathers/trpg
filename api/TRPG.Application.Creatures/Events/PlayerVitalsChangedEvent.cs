using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Results;

namespace TRPG.Application.Creatures.Events;

public record PlayerVitalsChangedEvent(CreatureVitals Vitals, long Version) : GameClientEvent;
