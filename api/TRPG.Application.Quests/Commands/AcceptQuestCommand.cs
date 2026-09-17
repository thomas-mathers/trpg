using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Commands;

public class AcceptQuestCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid QuestId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class AcceptQuestCommandHandler(
    IQuestsDbContext context,
    ICommandHandler<SetItemsCanTradeCommand> setItemsCanTrade,
    IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner,
    ICommandHandler<ReceivePlayerInventoryCommand> receivePlayerInventory
) : ICommandHandler<AcceptQuestCommand>
{
    public async Task Handle(
        AcceptQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var quest = await context.Quests.FirstOrDefaultAsync(
            quest => quest.Id == command.QuestId && quest.WorldId == command.WorldId,
            cancellationToken
        );
        if (quest is null || !quest.IsRevealed)
        {
            throw new EntityNotFoundException("Quest", command.QuestId);
        }

        var completedQuestIds = await context
            .CreatureQuests.Where(creatureQuest =>
                creatureQuest.CreatureId == command.PlayerId
                && creatureQuest.Status == QuestStatus.Completed
                && quest.PrerequisiteQuestIds.Contains(creatureQuest.QuestId)
            )
            .Select(creatureQuest => creatureQuest.QuestId)
            .ToArrayAsync(cancellationToken);
        if (completedQuestIds.Length != quest.PrerequisiteQuestIds.Count)
        {
            throw new InvalidOperationException("Quest prerequisites have not been completed.");
        }

        var objectiveIds = await context
            .QuestObjectives.AsNoTracking()
            .Where(objective => objective.QuestId == quest.Id)
            .Select(objective => objective.Id)
            .ToArrayAsync(cancellationToken);

        var collectItemIds = await context
            .QuestObjectives.AsNoTracking()
            .OfType<CollectItemObjective>()
            .Where(objective => objective.QuestId == quest.Id)
            .Select(objective => objective.ItemId)
            .ToArrayAsync(cancellationToken);

        var giveItemIdLists = await context
            .QuestObjectives.AsNoTracking()
            .OfType<GiveItemsObjective>()
            .Where(objective => objective.QuestId == quest.Id)
            .Select(objective => objective.ItemIds)
            .ToArrayAsync(cancellationToken);
        var giveItemIds = giveItemIdLists.SelectMany(itemIds => itemIds).ToArray();

        var deliverItemIds = await context
            .QuestObjectives.AsNoTracking()
            .OfType<DeliverItemObjective>()
            .Where(objective => objective.QuestId == quest.Id)
            .Select(objective => objective.ItemId)
            .ToArrayAsync(cancellationToken);

        var giverHandoffItemIds = giveItemIds.Concat(deliverItemIds).ToArray();
        var requiredItemIds = collectItemIds.Concat(giverHandoffItemIds).ToArray();

        context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = command.PlayerId,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                IsTracked = true,
                WorldId = command.WorldId,
            }
        );
        context.CreatureQuestObjectives.AddRange(
            objectiveIds.Select(objectiveId => new CreatureQuestObjective
            {
                CreatureId = command.PlayerId,
                ObjectiveId = objectiveId,
                Amount = 0,
                WorldId = command.WorldId,
            })
        );

        await context.SaveChangesAsync(cancellationToken);

        await setItemsCanTrade.Handle(
            new SetItemsCanTradeCommand { ItemIds = requiredItemIds, CanTrade = false },
            cancellationToken
        );

        await GiveGiverOwnedItemsToPlayer(
            command,
            quest.GiverId,
            giverHandoffItemIds,
            cancellationToken
        );
    }

    // A GiveItemsObjective or DeliverItemObjective's item doesn't always start with the player
    // (e.g. one recovered from a dungeon) — but when the giver is already holding it, accepting
    // the quest is them handing it over, same as a courier receiving a package from the person
    // who wants it delivered.
    private async Task GiveGiverOwnedItemsToPlayer(
        AcceptQuestCommand command,
        Guid giverId,
        IReadOnlyCollection<Guid> handoffItemIds,
        CancellationToken cancellationToken
    )
    {
        if (handoffItemIds.Count == 0)
        {
            return;
        }

        var giverOwnedItems = await getItemsByIdsForOwner.Handle(
            new GetItemsByIdsForOwnerQuery
            {
                OwnerId = giverId,
                OwnerType = OwnerType.Creature,
                ItemIds = handoffItemIds,
            },
            cancellationToken
        );
        if (giverOwnedItems.Count == 0)
        {
            return;
        }

        await receivePlayerInventory.Handle(
            new ReceivePlayerInventoryCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                From = new ItemOwnerReference(giverId, OwnerType.Creature),
                Items = giverOwnedItems.Select(item => new ItemSelection(item.Id, 1)).ToArray(),
            },
            cancellationToken
        );
    }
}
