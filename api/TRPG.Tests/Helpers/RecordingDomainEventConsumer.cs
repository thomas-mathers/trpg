using TRPG.Application.Common.Events;

namespace TRPG.Tests.Helpers;

internal sealed class RecordingDomainEventConsumer<TEvent> : IDomainEventConsumer<TEvent>
    where TEvent : DomainEvent
{
    public List<TEvent> Events { get; } = [];

    public Task Handle(TEvent domainEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(domainEvent);
        return Task.CompletedTask;
    }
}
