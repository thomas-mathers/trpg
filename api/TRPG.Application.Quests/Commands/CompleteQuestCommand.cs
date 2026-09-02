using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
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

internal class CompleteQuestCommandHandler(
    IQuestsDbContext context,
    IDomainEventPublisher<QuestGoldRewardedEvent> questGoldRewarded,
    IDomainEventPublisher<QuestReputationRewardedEvent> questReputationRewarded,
    IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner,
    ICommandHandler<SetItemsQuestLockedCommand> setItemsQuestLocked
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

        var requiredItemIds = await EnsureQuestItemsAreOwned(command, cancellationToken);

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

        await setItemsQuestLocked.Handle(
            new SetItemsQuestLockedCommand { ItemIds = requiredItemIds, IsQuestItem = false },
            cancellationToken
        );

        transaction.Complete();
    }

    private async Task<IReadOnlyCollection<Guid>> EnsureQuestItemsAreOwned(
        CompleteQuestCommand command,
        CancellationToken cancellationToken
    )
    {
        var requiredItemIds = await context
            .QuestObjectives.OfType<CollectItemObjective>()
            .Where(objective => objective.QuestId == command.QuestId)
            .Select(objective => objective.ItemId)
            .ToArrayAsync(cancellationToken);

        var ownedItems = await getItemsByIdsForOwner.Handle(
            new GetItemsByIdsForOwnerQuery
            {
                ItemIds = requiredItemIds,
                OwnerId = command.PlayerId,
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

        return requiredItemIds;
    }
}
