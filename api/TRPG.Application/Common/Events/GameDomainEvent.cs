using TRPG.Data.Models;

namespace TRPG.Application.Common.Events;

internal abstract record GameEvent(Guid PlayerId, Guid WorldId);

internal sealed record CreatureKilledEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid CreatureId,
    CreatureType CreatureType
) : GameEvent(PlayerId, WorldId);

internal sealed record PlayerMovedEvent(Guid PlayerId, Guid WorldId, Guid LocationId)
    : GameEvent(PlayerId, WorldId);

internal sealed record ConversationStartedEvent(Guid PlayerId, Guid WorldId, Guid CreatureId)
    : GameEvent(PlayerId, WorldId);

internal sealed record ItemAcquiredEvent(Guid PlayerId, Guid WorldId, Guid ItemId)
    : GameEvent(PlayerId, WorldId);
