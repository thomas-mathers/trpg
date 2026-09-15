using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Quests.Mappers;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public class GetQuestJournalQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

// One entry per distinct item name a GiveItemsObjective tracks — an objective whose ItemIds span
// several kinds of item (e.g. two Construct Gear and one Goblin Ear) reports each kind's own
// progress instead of only the objective's aggregate Amount/RequiredAmount.
public record QuestObjectiveItemProgress(string Name, int Amount, int RequiredAmount);

public record QuestObjectiveProgress(
    string Name,
    string Description,
    int Amount,
    int RequiredAmount,
    string? LocationName,
    IReadOnlyCollection<QuestObjectiveItemProgress>? Items
);

public record QuestJournalEntry(
    Guid Id,
    string Name,
    string Description,
    string? GiverName,
    int GoldReward,
    QuestStatus Status,
    bool IsTracked,
    IReadOnlyCollection<QuestObjectiveProgress> Objectives
);

internal class GetQuestJournalQueryHandler(
    IQuestsDbContext context,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<
        GetCreatureNamesByIdsQuery,
        IReadOnlyDictionary<Guid, string>
    > getCreatureNamesByIds,
    IQueryHandler<GetItemNamesByIdsQuery, IReadOnlyDictionary<Guid, string>> getItemNamesByIds,
    IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner
) : IQueryHandler<GetQuestJournalQuery, IReadOnlyCollection<QuestJournalEntry>>
{
    public async Task<IReadOnlyCollection<QuestJournalEntry>> Handle(
        GetQuestJournalQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var quests = await context
            .CreatureQuests.AsNoTracking()
            .Include(creatureQuest => creatureQuest.Quest)
            .Where(creatureQuest =>
                creatureQuest.CreatureId == query.PlayerId && creatureQuest.WorldId == query.WorldId
            )
            .OrderBy(creatureQuest => creatureQuest.Status)
            .ThenBy(creatureQuest => creatureQuest.Quest.Name)
            .ToArrayAsync(cancellationToken);

        var giverNamesById = await getCreatureNamesByIds.Handle(
            new GetCreatureNamesByIdsQuery
            {
                Ids = quests.Select(quest => quest.Quest.GiverId).Distinct().ToArray(),
            },
            cancellationToken
        );

        var objectives = await GetObjectives(query.PlayerId, query.WorldId, cancellationToken);

        var locationIds = objectives
            .Select(objective => objective.Objective.LocationId)
            .OfType<Guid>()
            .Distinct()
            .ToArray();

        var locationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );
        var locationNamesById = locationsById.ToDictionary(kv => kv.Key, kv => kv.Value.Name);

        var (itemNamesById, ownedItemIds) = await GetItemBreakdownLookups(
            query.PlayerId,
            query.WorldId,
            objectives,
            cancellationToken
        );

        var objectivesByQuestId = objectives
            .GroupBy(objective => objective.Objective.QuestId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .Select(objective =>
                            objective.ToProgress(
                                objective.Objective.LocationId is { } locationId
                                    ? locationNamesById.GetValueOrDefault(locationId)
                                    : null,
                                itemNamesById,
                                ownedItemIds
                            )
                        )
                        .ToArray()
            );

        return quests
            .Select(quest =>
                quest.ToJournalEntry(
                    giverNamesById.GetValueOrDefault(quest.Quest.GiverId),
                    objectivesByQuestId.GetValueOrDefault(quest.QuestId, [])
                )
            )
            .ToArray();
    }

    private Task<CreatureQuestObjective[]> GetObjectives(
        Guid playerId,
        Guid worldId,
        CancellationToken cancellationToken
    ) =>
        context
            .CreatureQuestObjectives.AsNoTracking()
            .Include(objective => objective.Objective)
            .Where(objective => objective.CreatureId == playerId && objective.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

    private async Task<(
        IReadOnlyDictionary<Guid, string> ItemNamesById,
        IReadOnlySet<Guid> OwnedItemIds
    )> GetItemBreakdownLookups(
        Guid playerId,
        Guid worldId,
        IReadOnlyCollection<CreatureQuestObjective> objectives,
        CancellationToken cancellationToken
    )
    {
        var itemIds = objectives
            .Select(objective => objective.Objective)
            .OfType<GiveItemsObjective>()
            .SelectMany(objective => objective.ItemIds)
            .Distinct()
            .ToArray();
        if (itemIds.Length == 0)
        {
            return (new Dictionary<Guid, string>(), new HashSet<Guid>());
        }

        var itemNamesById = await getItemNamesByIds.Handle(
            new GetItemNamesByIdsQuery { WorldId = worldId, ItemIds = itemIds },
            cancellationToken
        );
        var ownedItems = await getItemsByIdsForOwner.Handle(
            new GetItemsByIdsForOwnerQuery
            {
                OwnerId = playerId,
                OwnerType = OwnerType.Creature,
                ItemIds = itemIds,
            },
            cancellationToken
        );

        return (itemNamesById, ownedItems.Select(item => item.Id).ToHashSet());
    }
}
