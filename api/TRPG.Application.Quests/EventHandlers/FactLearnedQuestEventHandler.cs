using TRPG.Application.Common.Events;
using TRPG.Application.Quests.Events;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class FactLearnedQuestEventHandler(IGameClientEventSink gameEvents)
    : IDomainEventConsumer<FactLearnedEvent>
{
    public Task Handle(FactLearnedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        gameEvents.Enqueue(new QuestJournalUpdatedEvent());
        return Task.CompletedTask;
    }
}
