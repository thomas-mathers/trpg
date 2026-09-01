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
