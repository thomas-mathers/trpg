using TRPG.Application.Common.Events;

namespace TRPG.Application.Scenes.Events;

public record CaravansLeftEvent(Guid WorldId, IReadOnlySet<Guid> CaravanIds)
    : GameClientEvent(WorldId);
