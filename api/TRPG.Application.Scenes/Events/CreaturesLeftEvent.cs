using TRPG.Application.Common.Events;

namespace TRPG.Application.Scenes.Events;

public record CreaturesLeftEvent(Guid WorldId, IReadOnlySet<Guid> CreatureIds)
    : GameClientEvent(WorldId);
