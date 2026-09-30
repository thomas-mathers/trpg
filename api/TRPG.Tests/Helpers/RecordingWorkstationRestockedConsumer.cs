using TRPG.Application.Common.Events;

namespace TRPG.Tests.Helpers;

internal sealed class RecordingWorkstationRestockedConsumer
    : IDomainEventConsumer<WorkstationRestockedEvent>
{
    public List<WorkstationRestockedEvent> Events { get; } = [];

    public Task Handle(
        WorkstationRestockedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        Events.Add(domainEvent);
        return Task.CompletedTask;
    }
}
