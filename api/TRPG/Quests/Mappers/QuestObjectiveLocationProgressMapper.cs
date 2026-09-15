using TRPG.Application.Quests.Queries;
using TRPG.Quests.Responses;

namespace TRPG.Quests.Mappers;

internal static class QuestObjectiveLocationProgressMapper
{
    public static QuestObjectiveLocationProgressSnapshot ToSnapshot(
        this QuestObjectiveLocationProgress location
    ) => new(location.LocationName, location.RemainingCount);
}
