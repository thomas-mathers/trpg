using TRPG.Data.Models;

namespace TRPG.Application.Common.Events;

internal abstract record GameDomainEvent(Guid PlayerId, Guid WorldId);

internal sealed record CreatureKilledDomainEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid CreatureId,
    CreatureType CreatureType
) : GameDomainEvent(PlayerId, WorldId);

internal sealed record PlayerEnteredLocationDomainEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid LocationId
) : GameDomainEvent(PlayerId, WorldId);

internal sealed record ConversationStartedDomainEvent(Guid PlayerId, Guid WorldId, Guid CreatureId)
    : GameDomainEvent(PlayerId, WorldId);

internal sealed record ItemAcquiredDomainEvent(Guid PlayerId, Guid WorldId, Guid ItemId)
    : GameDomainEvent(PlayerId, WorldId);

internal record GameActionResult<T>(T Result, IReadOnlyCollection<GameDomainEvent> DomainEvents);

internal abstract class GameDomainEventListener
{
    public abstract Task Handle(
        GameDomainEvent domainEvent,
        CancellationToken cancellationToken = default
    );
}
