using TRPG.Application.Quests.Queries;
using TRPG.Quests.Responses;

namespace TRPG.Quests.Mappers;

internal static class QuestObjectiveProgressMapper
{
    public static QuestObjectiveProgressSnapshot ToSnapshot(this QuestObjectiveProgress progress) =>
        new(
            progress.Name,
            progress.Description,
            progress.Amount,
            progress.RequiredAmount,
            progress.LocationName,
            progress.Items?.Select(item => item.ToSnapshot()).ToArray(),
            progress.RemainingLocations?.Select(location => location.ToSnapshot()).ToArray()
        );
}
