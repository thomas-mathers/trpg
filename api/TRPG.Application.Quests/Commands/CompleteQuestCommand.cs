using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Commands;

public class CompleteQuestCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class CompleteQuestCommandHandler(
    TrpgDbContext context,
    ICommandHandler<AddGoldCommand> addGold,
    ICommandHandler<AdjustReputationsCommand> adjustReputations
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

        await EnsureQuestItemsAreOwned(command, cancellationToken);

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await addGold.Handle(
            new AddGoldCommand
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                WorldId = command.WorldId,
                Amount = creatureQuest.Quest.GoldReward,
            },
            cancellationToken
        );

        var rewardGroups = creatureQuest.Quest.ReputationRewards.GroupBy(reward =>
            reward.TargetType
        );

        foreach (var group in rewardGroups)
        {
            await adjustReputations.Handle(
                new AdjustReputationsCommand
                {
                    CreatureId = command.PlayerId,
                    Adjustments = group
                        .Select(reward => new ReputationAdjustment(reward.TargetId, reward.Score))
                        .ToArray(),
                    TargetType = group.Key,
                    Reason = ReputationReason.QuestCompleted,
                    Detail = $"Completed quest: {creatureQuest.Quest.Name}",
                },
                cancellationToken
            );
        }

        creatureQuest.Status = QuestStatus.Completed;
        creatureQuest.IsTracked = false;

        await context.SaveChangesAsync(cancellationToken);
        transaction.Complete();
    }

    private async Task EnsureQuestItemsAreOwned(
        CompleteQuestCommand command,
        CancellationToken cancellationToken
    )
    {
        var requiredItemIds = await context
            .QuestObjectives.OfType<CollectItemObjective>()
            .Where(objective => objective.QuestId == command.QuestId)
            .Select(objective => objective.ItemId)
            .ToArrayAsync(cancellationToken);

        var ownedItemIds = await context
            .Items.Where(item =>
                requiredItemIds.AsEnumerable().Contains(item.Id)
                && item.Ownership.OwnerId == command.PlayerId
                && item.Ownership.OwnerType == OwnerType.Creature
            )
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        var missingItemIds = requiredItemIds.Except(ownedItemIds).ToArray();

        if (missingItemIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Item {missingItemIds[0]} is required to complete this quest."
            );
        }
    }
}
