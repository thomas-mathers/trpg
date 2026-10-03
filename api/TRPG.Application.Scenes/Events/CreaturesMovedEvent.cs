using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Events;

public record CreaturesMovedEvent(
    Guid WorldId,
    IReadOnlyDictionary<Guid, Placement> CreaturePlacements
) : GameClientEvent(WorldId);
