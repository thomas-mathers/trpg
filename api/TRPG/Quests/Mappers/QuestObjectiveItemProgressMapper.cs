using TRPG.Application.Quests.Queries;
using TRPG.Quests.Responses;

namespace TRPG.Quests.Mappers;

internal static class QuestObjectiveItemProgressMapper
{
    public static QuestObjectiveItemProgressSnapshot ToSnapshot(
        this QuestObjectiveItemProgress item
    ) => new(item.Name, item.Amount, item.RequiredAmount);
}
