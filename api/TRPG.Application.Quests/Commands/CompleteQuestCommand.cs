using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Crimes.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Knowledge.Commands;
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

internal record GiveItemKindRequirement(string ItemName, int RequiredAmount, Guid RecipientId);

internal class CompleteQuestCommandHandler(
    IQuestsDbContext context,
    IDomainEventPublisher<QuestGoldRewardedEvent> questGoldRewarded,
    IDomainEventPublisher<QuestReputationRewardedEvent> questReputationRewarded,
    IDomainEventPublisher<QuestCompletedEvent> questCompleted,
    IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner,
    IQueryHandler<GetItemsByNameForOwnerQuery, IReadOnlyList<Item>> getItemsByNameForOwner,
    IQueryHandler<GetReportedStolenItemIdsQuery, IReadOnlySet<Guid>> getReportedStolenItemIds,
    ICommandHandler<SetItemsCanTradeCommand> setItemsCanTrade,
    ICommandHandler<TransferPlayerInventoryCommand> transferPlayerInventory,
    ICommandHandler<LearnFactCommand, bool> learnFact
) : ICommandHandler<CompleteQuestCommand>
{
    // A witnessed-and-reported theft of one of this quest's own items halves the payout, flat
    // and quest-wide regardless of how many items were caught — not steal-quest-specific: any
    // future quest whose items get stolen picks up the same penalty for free.
    private const double CaughtRewardMultiplier = 0.5;

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

        var giveItemGroups = await context
            .QuestObjectives.OfType<GiveItemsObjective>()
            .Where(objective => objective.QuestId == command.QuestId)
            .Select(objective => new { objective.ItemIds, objective.RecipientId })
            .ToArrayAsync(cancellationToken);
        var giveItems = giveItemGroups
            .SelectMany(group =>
                group.ItemIds.Select(itemId => new GiveItemRequirement(itemId, group.RecipientId))
            )
            .ToArray();

        var requiredItemIds = collectItemIds
            .Concat(giveItems.Select(giveItem => giveItem.ItemId))
            .ToArray();

        await EnsureItemsAreOwned(command.PlayerId, requiredItemIds, cancellationToken);

        var giveItemKindRequirements = await context
            .QuestObjectives.OfType<GiveItemKindObjective>()
            .Where(objective => objective.QuestId == command.QuestId)
            .Select(objective => new GiveItemKindRequirement(
                objective.ItemName,
                objective.RequiredAmount,
                objective.RecipientId
            ))
            .ToArrayAsync(cancellationToken);
        var giveItemKindTransfers = await ResolveGiveItemKindTransfers(
            command.PlayerId,
            command.WorldId,
            giveItemKindRequirements,
            cancellationToken
        );
        var allGiveItems = giveItems.Concat(giveItemKindTransfers).ToArray();

        var reportFacts = await context
            .QuestObjectives.OfType<ReportFactToCreatureObjective>()
            .Where(objective => objective.QuestId == command.QuestId)
            .Select(objective => new { objective.CreatureId, objective.FactId })
            .ToArrayAsync(cancellationToken);

        var rewardMultiplier = await GetRewardMultiplier(
            command.PlayerId,
            command.WorldId,
            giveItems,
            cancellationToken
        );

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await questGoldRewarded.Publish(
            new QuestGoldRewardedEvent(
                command.PlayerId,
                command.WorldId,
                (int)(creatureQuest.Quest.GoldReward * rewardMultiplier)
            ),
            cancellationToken
        );

        await questReputationRewarded.Publish(
            new QuestReputationRewardedEvent(
                command.PlayerId,
                command.WorldId,
                ScaleReputationRewards(creatureQuest.Quest.ReputationRewards, rewardMultiplier),
                $"Completed quest: {creatureQuest.Quest.Name}"
            ),
            cancellationToken
        );

        creatureQuest.Status = QuestStatus.Completed;
        creatureQuest.IsTracked = false;

        await context.SaveChangesAsync(cancellationToken);

        await questCompleted.Publish(
            new QuestCompletedEvent(command.PlayerId, command.WorldId, command.QuestId),
            cancellationToken
        );

        await setItemsCanTrade.Handle(
            new SetItemsCanTradeCommand { ItemIds = requiredItemIds, CanTrade = true },
            cancellationToken
        );

        foreach (var recipientItems in allGiveItems.GroupBy(giveItem => giveItem.RecipientId))
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

        foreach (var reportFact in reportFacts)
        {
            await learnFact.Handle(
                new LearnFactCommand
                {
                    WorldId = command.WorldId,
                    KnowerId = reportFact.CreatureId,
                    FactId = reportFact.FactId,
                },
                cancellationToken
            );
        }

        transaction.Complete();
    }

    private async Task<double> GetRewardMultiplier(
        Guid playerId,
        Guid worldId,
        IReadOnlyCollection<GiveItemRequirement> giveItems,
        CancellationToken cancellationToken
    )
    {
        if (giveItems.Count == 0)
        {
            return 1.0;
        }

        var caughtItemIds = await getReportedStolenItemIds.Handle(
            new GetReportedStolenItemIdsQuery
            {
                WorldId = worldId,
                PlayerId = playerId,
                ItemIds = giveItems.Select(giveItem => giveItem.ItemId).ToArray(),
            },
            cancellationToken
        );

        return caughtItemIds.Count > 0 ? CaughtRewardMultiplier : 1.0;
    }

    private static IReadOnlyCollection<QuestReputationReward> ScaleReputationRewards(
        IReadOnlyCollection<QuestReputationReward> rewards,
        double multiplier
    ) =>
        multiplier >= 1.0
            ? rewards
            : rewards
                .Select(reward => new QuestReputationReward
                {
                    WorldId = reward.WorldId,
                    QuestId = reward.QuestId,
                    TargetId = reward.TargetId,
                    TargetType = reward.TargetType,
                    Score = (int)(reward.Score * multiplier),
                })
                .ToArray();

    private async Task<IReadOnlyCollection<GiveItemRequirement>> ResolveGiveItemKindTransfers(
        Guid playerId,
        Guid worldId,
        IReadOnlyCollection<GiveItemKindRequirement> requirements,
        CancellationToken cancellationToken
    )
    {
        var transfers = new List<GiveItemRequirement>();

        foreach (var requirement in requirements)
        {
            var ownedItems = await getItemsByNameForOwner.Handle(
                new GetItemsByNameForOwnerQuery
                {
                    WorldId = worldId,
                    OwnerId = playerId,
                    OwnerType = OwnerType.Creature,
                    Name = requirement.ItemName,
                },
                cancellationToken
            );

            if (ownedItems.Count < requirement.RequiredAmount)
            {
                throw new InvalidOperationException(
                    $"{requirement.RequiredAmount}x {requirement.ItemName} is required to complete this quest."
                );
            }

            transfers.AddRange(
                ownedItems
                    .Take(requirement.RequiredAmount)
                    .Select(item => new GiveItemRequirement(item.Id, requirement.RecipientId))
            );
        }

        return transfers;
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
