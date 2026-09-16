using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
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

// Where a GiveItemsObjective's still-uncollected items currently are, resolved live from each
// item's current owner rather than a static field — an owning creature can move between visits.
public record QuestObjectiveLocationProgress(string LocationName, int RemainingCount);

public record QuestObjectiveProgress(
    string Name,
    string Description,
    int Amount,
    int RequiredAmount,
    string? LocationName,
    IReadOnlyCollection<QuestObjectiveItemProgress>? Items,
    IReadOnlyCollection<QuestObjectiveLocationProgress>? RemainingLocations
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
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetPropsByIdsQuery, IReadOnlyDictionary<Guid, Prop>> getPropsByIds,
    IQueryHandler<GetItemNamesByIdsQuery, IReadOnlyDictionary<Guid, string>> getItemNamesByIds,
    IQueryHandler<GetItemsByIdsQuery, IReadOnlyDictionary<Guid, Item>> getItemsByIds,
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

        var (itemNamesById, ownedItemIds) = await GetItemBreakdownLookups(
            query.PlayerId,
            query.WorldId,
            objectives,
            cancellationToken
        );

        var remainingLocationCountsByObjectiveId = await GetRemainingItemLocationCounts(
            objectives,
            ownedItemIds,
            cancellationToken
        );

        var staticLocationIds = objectives
            .Select(objective => objective.Objective.LocationId)
            .OfType<Guid>();
        var remainingLocationIds = remainingLocationCountsByObjectiveId.Values.SelectMany(
            countsByLocationId => countsByLocationId.Keys
        );
        var locationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery
            {
                Ids = staticLocationIds.Concat(remainingLocationIds).Distinct().ToArray(),
            },
            cancellationToken
        );
        var locationNamesById = locationsById.ToDictionary(kv => kv.Key, kv => kv.Value.Name);

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
                                ownedItemIds,
                                BuildRemainingLocations(
                                    remainingLocationCountsByObjectiveId.GetValueOrDefault(
                                        objective.ObjectiveId
                                    ),
                                    locationNamesById
                                )
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

    // For each GiveItemsObjective, how many of its still-unowned items currently sit at each
    // location — resolved from each item's live owner (a creature's current LocationId, or a
    // container/workstation's fixed one), never a value stored on the objective itself.
    private async Task<
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, int>>
    > GetRemainingItemLocationCounts(
        IReadOnlyCollection<CreatureQuestObjective> objectives,
        IReadOnlySet<Guid> ownedItemIds,
        CancellationToken cancellationToken
    )
    {
        var remainingItemIdsByObjectiveId = objectives
            .Where(objective => objective.Objective is GiveItemsObjective)
            .ToDictionary(
                objective => objective.ObjectiveId,
                objective =>
                    ((GiveItemsObjective)objective.Objective)
                        .ItemIds.Where(itemId => !ownedItemIds.Contains(itemId))
                        .ToArray()
            );

        var allRemainingItemIds = remainingItemIdsByObjectiveId
            .Values.SelectMany(itemIds => itemIds)
            .Distinct()
            .ToArray();
        if (allRemainingItemIds.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>();
        }

        var itemsById = await getItemsByIds.Handle(
            new GetItemsByIdsQuery { Ids = allRemainingItemIds },
            cancellationToken
        );

        var locationIdByItemId = await ResolveItemLocationIds(itemsById, cancellationToken);

        return remainingItemIdsByObjectiveId.ToDictionary(
            pair => pair.Key,
            pair =>
                (IReadOnlyDictionary<Guid, int>)
                    pair
                        .Value.Where(locationIdByItemId.ContainsKey)
                        .GroupBy(itemId => locationIdByItemId[itemId])
                        .ToDictionary(group => group.Key, group => group.Count())
        );
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> ResolveItemLocationIds(
        IReadOnlyDictionary<Guid, Item> itemsById,
        CancellationToken cancellationToken
    )
    {
        var creatureOwnerIdByItemId = itemsById
            .Values.Where(item => item.Ownership.OwnerType == OwnerType.Creature)
            .ToDictionary(item => item.Id, item => item.Ownership.OwnerId);
        var propOwnerIdByItemId = itemsById
            .Values.Where(item => item.Ownership.OwnerType != OwnerType.Creature)
            .ToDictionary(item => item.Id, item => item.Ownership.OwnerId);

        var creaturesById =
            creatureOwnerIdByItemId.Count == 0
                ? new Dictionary<Guid, Creature>()
                : await getCreaturesByIds.Handle(
                    new GetCreaturesByIdsQuery
                    {
                        Ids = creatureOwnerIdByItemId.Values.Distinct().ToArray(),
                    },
                    cancellationToken
                );
        var propsById =
            propOwnerIdByItemId.Count == 0
                ? new Dictionary<Guid, Prop>()
                : await getPropsByIds.Handle(
                    new GetPropsByIdsQuery
                    {
                        Ids = propOwnerIdByItemId.Values.Distinct().ToArray(),
                    },
                    cancellationToken
                );

        var locationIdByItemId = new Dictionary<Guid, Guid>();
        foreach (var (itemId, ownerId) in creatureOwnerIdByItemId)
        {
            if (creaturesById.TryGetValue(ownerId, out var creature))
            {
                locationIdByItemId[itemId] = creature.LocationId;
            }
        }
        foreach (var (itemId, ownerId) in propOwnerIdByItemId)
        {
            if (propsById.TryGetValue(ownerId, out var prop))
            {
                locationIdByItemId[itemId] = prop.LocationId;
            }
        }

        return locationIdByItemId;
    }

    private static IReadOnlyCollection<QuestObjectiveLocationProgress>? BuildRemainingLocations(
        IReadOnlyDictionary<Guid, int>? remainingCountsByLocationId,
        IReadOnlyDictionary<Guid, string> locationNamesById
    ) =>
        remainingCountsByLocationId is null || remainingCountsByLocationId.Count == 0
            ? null
            : remainingCountsByLocationId
                .Select(pair => new QuestObjectiveLocationProgress(
                    locationNamesById.GetValueOrDefault(pair.Key, "an unknown location"),
                    pair.Value
                ))
                .ToArray();
}
