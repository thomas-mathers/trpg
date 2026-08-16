using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Events;

internal sealed class ItemAcquiredQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
    : IDomainEventConsumer<ItemAcquiredEvent>
{
    public Task Handle(
        ItemAcquiredEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective is CollectItemObjective collect && collect.ItemId == domainEvent.ItemId,
            cancellationToken
        );
}
