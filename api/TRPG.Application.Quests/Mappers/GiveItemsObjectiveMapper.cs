using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Mappers;

internal static class GiveItemsObjectiveMapper
{
    public static IReadOnlyCollection<QuestObjectiveItemProgress> ToItemProgress(
        this GiveItemsObjective objective,
        IReadOnlyDictionary<Guid, string> itemNamesById,
        IReadOnlySet<Guid> ownedItemIds
    ) =>
        objective
            .ItemIds.GroupBy(itemId => itemNamesById.GetValueOrDefault(itemId, "Unknown Item"))
            .Select(group => new QuestObjectiveItemProgress(
                group.Key,
                group.Count(itemId => ownedItemIds.Contains(itemId)),
                group.Count()
            ))
            .ToArray();
}
