using TRPG.Domain.Models;

namespace TRPG.Application.Common.Events;

public abstract record DomainEvent;

public interface IDomainEventConsumer<in TEvent>
    where TEvent : DomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IDomainEventPublisher<in TEvent>
    where TEvent : DomainEvent
{
    Task Publish(TEvent domainEvent, CancellationToken cancellationToken = default);
}

public sealed class DomainEventPublisher<TEvent>(IEnumerable<IDomainEventConsumer<TEvent>> handlers)
    : IDomainEventPublisher<TEvent>
    where TEvent : DomainEvent
{
    public async Task Publish(TEvent domainEvent, CancellationToken cancellationToken = default)
    {
        foreach (var handler in handlers)
        {
            await handler.Handle(domainEvent, cancellationToken);
        }
    }
}

public sealed record NpcConversationStartedEvent(Guid PlayerId, Guid WorldId, Guid NpcId)
    : DomainEvent;

public sealed record CreatureKilledEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid CreatureId,
    CreatureType CreatureType
) : DomainEvent;

public sealed record PlayerMovedEvent(Guid PlayerId, Guid WorldId, Guid LocationId) : DomainEvent;

public sealed record ItemAcquiredEvent(Guid PlayerId, Guid WorldId, Guid ItemId) : DomainEvent;

public sealed record CreatureEquipmentChangedEvent(Guid CreatureId) : DomainEvent;

public sealed record QuestGoldRewardedEvent(Guid PlayerId, Guid WorldId, int Amount) : DomainEvent;

public sealed record QuestReputationRewardedEvent(
    Guid PlayerId,
    Guid WorldId,
    IReadOnlyCollection<QuestReputationReward> Rewards,
    string Detail
) : DomainEvent;
