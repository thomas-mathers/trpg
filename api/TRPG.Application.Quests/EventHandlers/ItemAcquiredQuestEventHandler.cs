using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class ItemAcquiredQuestEventHandler(
    QuestObjectiveAdvancer questObjectiveAdvancer,
    IQueryHandler<GetItemNamesByIdsQuery, IReadOnlyDictionary<Guid, string>> getItemNamesByIds
) : IDomainEventConsumer<ItemAcquiredEvent>
{
    public async Task Handle(
        ItemAcquiredEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var itemNamesById = await getItemNamesByIds.Handle(
            new GetItemNamesByIdsQuery
            {
                WorldId = domainEvent.WorldId,
                ItemIds = [domainEvent.ItemId],
            },
            cancellationToken
        );
        var itemName = itemNamesById.GetValueOrDefault(domainEvent.ItemId);

        await questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                (objective is CollectItemObjective collect && collect.ItemId == domainEvent.ItemId)
                || (
                    objective is GiveItemsObjective give
                    && give.ItemIds.Contains(domainEvent.ItemId)
                )
                || (
                    objective is GiveItemKindObjective kind
                    && itemName != null
                    && kind.ItemName == itemName
                ),
            cancellationToken
        );
    }
}
