using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Commands;

public class CompleteQuestCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
    public required Guid WorldId { get; init; }
}

internal record GiveItemRequirement(Guid ItemId, Guid RecipientId);

internal class CompleteQuestCommandHandler(
    IQuestsDbContext context,
    IDomainEventPublisher<QuestGoldRewardedEvent> questGoldRewarded,
    IDomainEventPublisher<QuestReputationRewardedEvent> questReputationRewarded,
    IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner,
    ICommandHandler<SetItemsCanTradeCommand> setItemsCanTrade,
    ICommandHandler<TransferPlayerInventoryCommand> transferPlayerInventory
) : ICommandHandler<CompleteQuestCommand>
{
    public async Task Handle(
        CompleteQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creatureQuest = await context
            .CreatureQuests.Include(quest => quest.Quest)
                .ThenInclude(quest => quest.ReputationRewards)
            .FirstOrDefaultAsync(
                quest =>
                    quest.CreatureId == command.PlayerId
                    && quest.QuestId == command.QuestId
                    && quest.WorldId == command.WorldId,
                cancellationToken
            );

        if (creatureQuest is null)
        {
            throw new EntityNotFoundException("Accepted quest", command.QuestId);
        }

        if (creatureQuest.Status != QuestStatus.ReadyToComplete)
        {
            throw new InvalidOperationException("Quest objectives have not all been completed.");
        }

        var collectItemIds = await context
            .QuestObjectives.OfType<CollectItemObjective>()
            .Where(objective => objective.QuestId == command.QuestId)
            .Select(objective => objective.ItemId)
            .ToArrayAsync(cancellationToken);

        var giveItems = await context
            .QuestObjectives.OfType<GiveItemObjective>()
            .Where(objective => objective.QuestId == command.QuestId)
            .Select(objective => new GiveItemRequirement(objective.ItemId, objective.RecipientId))
            .ToArrayAsync(cancellationToken);

        var requiredItemIds = collectItemIds
            .Concat(giveItems.Select(giveItem => giveItem.ItemId))
            .ToArray();

        await EnsureItemsAreOwned(command.PlayerId, requiredItemIds, cancellationToken);

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await questGoldRewarded.Publish(
            new QuestGoldRewardedEvent(
                command.PlayerId,
                command.WorldId,
                creatureQuest.Quest.GoldReward
            ),
            cancellationToken
        );

        await questReputationRewarded.Publish(
            new QuestReputationRewardedEvent(
                command.PlayerId,
                command.WorldId,
                creatureQuest.Quest.ReputationRewards,
                $"Completed quest: {creatureQuest.Quest.Name}"
            ),
            cancellationToken
        );

        creatureQuest.Status = QuestStatus.Completed;
        creatureQuest.IsTracked = false;

        await context.SaveChangesAsync(cancellationToken);

        await setItemsCanTrade.Handle(
            new SetItemsCanTradeCommand { ItemIds = requiredItemIds, CanTrade = true },
            cancellationToken
        );

        foreach (var recipientItems in giveItems.GroupBy(giveItem => giveItem.RecipientId))
        {
            await transferPlayerInventory.Handle(
                new TransferPlayerInventoryCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = command.PlayerId,
                    To = new ItemOwnerReference(recipientItems.Key, OwnerType.Creature),
                    Items = recipientItems
                        .Select(giveItem => new ItemSelection(giveItem.ItemId, 1))
                        .ToArray(),
                },
                cancellationToken
            );
        }

        transaction.Complete();
    }

    private async Task EnsureItemsAreOwned(
        Guid playerId,
        IReadOnlyCollection<Guid> requiredItemIds,
        CancellationToken cancellationToken
    )
    {
        if (requiredItemIds.Count == 0)
        {
            return;
        }

        var ownedItems = await getItemsByIdsForOwner.Handle(
            new GetItemsByIdsForOwnerQuery
            {
                ItemIds = requiredItemIds,
                OwnerId = playerId,
                OwnerType = OwnerType.Creature,
            },
            cancellationToken
        );
        var ownedItemIds = ownedItems.Select(item => item.Id).ToArray();

        var missingItemIds = requiredItemIds.Except(ownedItemIds).ToArray();

        if (missingItemIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Item {missingItemIds[0]} is required to complete this quest."
            );
        }
    }
}
