using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class ItemGivenToCreatureQuestEventHandler(
    QuestObjectiveAdvancer questObjectiveAdvancer
) : IDomainEventConsumer<ItemGivenToCreatureEvent>
{
    public Task Handle(
        ItemGivenToCreatureEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective is DeliverItemObjective deliver
                && deliver.ItemId == domainEvent.ItemId
                && deliver.RecipientId == domainEvent.RecipientId,
            cancellationToken
        );
}
