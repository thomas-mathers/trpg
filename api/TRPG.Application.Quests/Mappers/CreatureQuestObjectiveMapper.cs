using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Mappers;

internal static class CreatureQuestObjectiveMapper
{
    public static QuestObjectiveProgress ToProgress(
        this CreatureQuestObjective objective,
        string? locationName,
        IReadOnlyDictionary<Guid, string> itemNamesById,
        IReadOnlySet<Guid> ownedItemIds,
        IReadOnlyCollection<QuestObjectiveLocationProgress>? remainingLocations
    ) =>
        new(
            objective.Objective.Name,
            objective.Objective.Description,
            objective.Amount,
            objective.Objective.RequiredAmount,
            locationName,
            objective.Objective is GiveItemsObjective giveItems
                ? giveItems.ToItemProgress(itemNamesById, ownedItemIds)
                : null,
            remainingLocations
        );
}
